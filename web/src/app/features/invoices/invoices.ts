import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../../core/api';
import { InvoiceDetail, InvoiceListItem, InvoiceStatus, Paged } from '../../core/api.types';
import { formatDateTime, formatMoney } from '../../core/format';
import { ErrorMessage, describeError } from '../../core/problem';

@Component({
  selector: 'app-invoices',
  imports: [FormsModule],
  templateUrl: './invoices.html',
  styleUrl: './invoices.css',
})
export class Invoices implements OnInit {
  private readonly api = inject(Api);

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatMoney = formatMoney;

  protected readonly status = signal<InvoiceStatus | ''>('');
  protected readonly page = signal(1);
  protected readonly result = signal<Paged<InvoiceListItem> | null>(null);
  protected readonly selected = signal<InvoiceDetail | null>(null);
  protected readonly error = signal<ErrorMessage | null>(null);
  protected readonly busy = signal(false);
  protected readonly paying = signal(false);

  protected readonly totalPages = computed(() => {
    const r = this.result();
    return r ? Math.max(1, Math.ceil(r.totalCount / r.pageSize)) : 1;
  });

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected async setStatus(value: InvoiceStatus | ''): Promise<void> {
    this.status.set(value);
    this.page.set(1);
    await this.load();
  }

  protected async goTo(page: number): Promise<void> {
    this.page.set(Math.min(Math.max(1, page), this.totalPages()));
    await this.load();
  }

  protected async open(invoice: InvoiceListItem): Promise<void> {
    this.error.set(null);
    try {
      this.selected.set(await this.api.invoice(invoice.id));
    } catch (error) {
      this.error.set(describeError(error));
    }
  }

  protected async pay(invoice: InvoiceDetail): Promise<void> {
    this.paying.set(true);
    this.error.set(null);
    try {
      this.selected.set(await this.api.payInvoice(invoice.id));
      await this.load();   // the list shows the new status
    } catch (error) {
      this.error.set(describeError(error));   // for example "already paid" if someone else got there first
    } finally {
      this.paying.set(false);
    }
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      this.result.set(await this.api.invoices(this.status(), this.page()));
    } catch (error) {
      this.error.set(describeError(error));
    } finally {
      this.busy.set(false);
    }
  }
}
