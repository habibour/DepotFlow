import { Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Api } from '../../core/api';
import { ShippingLine, Visit, VisitListItem } from '../../core/api.types';
import { checkContainerNumber } from '../../core/container-number';
import { formatDateTime, formatMoney } from '../../core/format';
import { ErrorMessage, describeError } from '../../core/problem';

/** Live check of the container number field, using the client copy of the ISO 6346 rule. */
export function containerNumberValidator(control: AbstractControl): ValidationErrors | null {
  const result = checkContainerNumber(control.value as string);
  return result.valid ? null : { container: result.error };
}

@Component({
  selector: 'app-gate',
  imports: [ReactiveFormsModule],
  templateUrl: './gate.html',
  styleUrl: './gate.css',
})
export class Gate implements OnInit {
  private readonly api = inject(Api);
  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatMoney = formatMoney;

  protected readonly lines = signal<ShippingLine[]>([]);
  protected readonly inYard = signal<VisitListItem[]>([]);
  protected readonly inYardTotal = signal(0);
  protected readonly loadError = signal<ErrorMessage | null>(null);

  protected readonly gateInBusy = signal(false);
  protected readonly gateInError = signal<ErrorMessage | null>(null);
  protected readonly gateInResult = signal<Visit | null>(null);

  protected readonly gatingOutId = signal<number | null>(null);
  protected readonly gateOutBusy = signal(false);
  protected readonly gateOutError = signal<ErrorMessage | null>(null);
  protected readonly released = signal<Visit | null>(null);

  protected readonly form = this.fb.group({
    containerNumber: ['', [containerNumberValidator]],
    sizeFeet: [20, Validators.required],
    shippingLineId: [0, [Validators.required, Validators.min(1)]],
    truckNumber: ['', [Validators.required, Validators.maxLength(30)]],
    sealNumber: ['', [Validators.required, Validators.maxLength(30)]],
    damageNotes: ['', Validators.maxLength(500)],
  });

  protected readonly outForm = this.fb.group({
    truckNumber: ['', [Validators.required, Validators.maxLength(30)]],
    damageNotes: ['', Validators.maxLength(500)],
  });

  async ngOnInit(): Promise<void> {
    try {
      const [lines] = await Promise.all([this.api.shippingLines(), this.refreshInYard()]);
      this.lines.set(lines.items);
    } catch (error) {
      this.loadError.set(describeError(error));
    }
  }

  protected async gateIn(): Promise<void> {
    if (this.form.invalid || this.gateInBusy()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.gateInBusy.set(true);
    this.gateInError.set(null);
    this.gateInResult.set(null);
    try {
      const visit = await this.api.gateIn({
        containerNumber: checkContainerNumber(value.containerNumber).value ?? value.containerNumber,
        sizeFeet: Number(value.sizeFeet),
        shippingLineId: Number(value.shippingLineId),
        truckNumber: value.truckNumber.trim(),
        sealNumber: value.sealNumber.trim(),
        damageNotes: value.damageNotes.trim() || null,
      });
      this.gateInResult.set(visit);
      // reset() clears the dirty and touched flags too, so the empty fields do not light up as errors.
      // Size and shipping line stay as they were: the next container at the gate is often from the same line.
      this.form.reset({ containerNumber: '', sizeFeet: value.sizeFeet, shippingLineId: value.shippingLineId, truckNumber: '', sealNumber: '', damageNotes: '' });
      await this.refreshInYard();
    } catch (error) {
      // The server is the authority: show its explanation (no tariff, yard full, already in the yard, ...).
      this.gateInError.set(describeError(error));
    } finally {
      this.gateInBusy.set(false);
    }
  }

  protected startGateOut(visit: VisitListItem): void {
    this.gatingOutId.set(this.gatingOutId() === visit.id ? null : visit.id);
    this.gateOutError.set(null);
    this.outForm.reset({ truckNumber: '', damageNotes: '' });
  }

  protected async confirmGateOut(visit: VisitListItem): Promise<void> {
    if (this.outForm.invalid || this.gateOutBusy()) {
      this.outForm.markAllAsTouched();
      return;
    }

    const value = this.outForm.getRawValue();
    this.gateOutBusy.set(true);
    this.gateOutError.set(null);
    try {
      this.released.set(await this.api.gateOut(visit.id, { truckNumber: value.truckNumber.trim(), damageNotes: value.damageNotes.trim() || null }));
      this.gatingOutId.set(null);
      await this.refreshInYard();
    } catch (error) {
      this.gateOutError.set(describeError(error));
    } finally {
      this.gateOutBusy.set(false);
    }
  }

  private async refreshInYard(): Promise<void> {
    const page = await this.api.inYardVisits();
    this.inYard.set(page.items);
    this.inYardTotal.set(page.totalCount);
  }
}
