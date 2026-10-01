import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { Api } from '../../core/api';
import { Paged, ShippingLine, Visit, VisitListItem } from '../../core/api.types';
import { Gate } from './gate';

const lines: Paged<ShippingLine> = {
  items: [{ id: 3, code: 'CMDU', name: 'CMA CGM', isActive: true }],
  page: 1, pageSize: 100, totalCount: 1,
};

const inYard = (items: VisitListItem[] = []): Paged<VisitListItem> => ({ items, page: 1, pageSize: 50, totalCount: items.length });

const visit = (overrides: Partial<Visit> = {}): Visit => ({
  id: 7, containerId: 1, containerNumber: 'CSQU3054383', sizeFeet: 20,
  shippingLine: { id: 3, code: 'CMDU', name: 'CMA CGM' }, yardSlot: { id: 1, code: 'A-01-01-1' },
  status: 'InYard', gateInAtUtc: '2026-10-01T04:00:00Z', gateOutAtUtc: null, truckInNumber: 'T1', truckOutNumber: null,
  sealNumber: 'S1', dwellDays: null, invoice: null, ...overrides,
});

async function setup(api: Partial<Record<keyof Api, unknown>>) {
  TestBed.configureTestingModule({
    imports: [Gate],
    providers: [{ provide: Api, useValue: { shippingLines: () => Promise.resolve(lines), inYardVisits: () => Promise.resolve(inYard()), ...api } }],
  });
  const fixture = TestBed.createComponent(Gate);
  await fixture.whenStable();
  const el = fixture.nativeElement as HTMLElement;
  // The form is protected API of the component; tests fill it directly and submit through the DOM.
  const form = (fixture.componentInstance as unknown as { form: { patchValue(v: object): void; controls: Record<string, { markAsDirty(): void }> } }).form;
  return { fixture, el, form };
}

const submit = (el: HTMLElement) => el.querySelector('form')!.dispatchEvent(new Event('submit'));

const validFields = { containerNumber: 'csqu3054383', sizeFeet: 20, shippingLineId: 3, truckNumber: 'DHK-TA-11-2345', sealNumber: 'SL889201' };

describe('Gate', () => {
  it('shows the reason as soon as the container number is wrong', async () => {
    const { fixture, el } = await setup({});
    const input = el.querySelector<HTMLInputElement>('#containerNumber')!;

    input.value = 'CSQU3054384';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(el.querySelector('.hint')?.textContent).toContain('Check digit is wrong; expected 3.');
  });

  it('does not call the API while the form is invalid', async () => {
    const gateIn = vi.fn();
    const { fixture, el } = await setup({ gateIn });

    submit(el);
    await fixture.whenStable();

    expect(gateIn).not.toHaveBeenCalled();
  });

  it('gates in with the normalised container number and shows the slot', async () => {
    const gateIn = vi.fn().mockResolvedValue(visit());
    const { fixture, el, form } = await setup({ gateIn });
    form.patchValue(validFields);

    submit(el);
    await fixture.whenStable();

    expect(gateIn).toHaveBeenCalledWith({
      containerNumber: 'CSQU3054383', sizeFeet: 20, shippingLineId: 3, truckNumber: 'DHK-TA-11-2345', sealNumber: 'SL889201', damageNotes: null,
    });
    expect(el.querySelector('.alert.success')?.textContent).toContain('A-01-01-1');
  });

  it('clears the form after a gate-in without showing validation errors on the empty fields', async () => {
    const { fixture, el, form } = await setup({ gateIn: vi.fn().mockResolvedValue(visit()) });
    form.patchValue(validFields);
    el.querySelector<HTMLInputElement>('#containerNumber')!.dispatchEvent(new Event('input'));   // the field was edited: dirty

    submit(el);
    await fixture.whenStable();

    expect(el.querySelector<HTMLInputElement>('#containerNumber')!.value).toBe('');
    expect(el.querySelectorAll('.hint').length).toBe(0);
    expect(el.querySelector<HTMLSelectElement>('#shippingLineId')!.selectedOptions[0].textContent).toContain('CMDU');   // kept for the next container
  });

  it('shows the server explanation when gate-in is refused', async () => {
    const refusal = new HttpErrorResponse({ status: 422, error: { title: 'There is no active tariff for this shipping line and container size.', code: 'no_active_tariff', traceId: 'abc' } });
    const { fixture, el, form } = await setup({ gateIn: vi.fn().mockRejectedValue(refusal) });
    form.patchValue(validFields);

    submit(el);
    await fixture.whenStable();

    expect(el.querySelector('.alert.error')?.textContent).toContain('no active tariff');
    expect(el.querySelector('.alert.error')?.textContent).toContain('abc');   // the trace id, to quote when reporting it
  });

  it('lists the containers in the yard and releases one, showing the invoice', async () => {
    const row: VisitListItem = {
      id: 7, containerId: 1, containerNumber: 'CSQU3054383', sizeFeet: 20, shippingLineCode: 'CMDU', slotCode: 'A-01-01-1',
      status: 'InYard', gateInAtUtc: '2026-10-01T04:00:00Z', gateOutAtUtc: null, dwellDays: 12,
    };
    const released = visit({ status: 'Released', yardSlot: null, dwellDays: 12, invoice: { id: 1, invoiceNumber: 'INV-2026-0000001', total: 2000, currency: 'BDT', status: 'Issued' } });
    const gateOut = vi.fn().mockResolvedValue(released);
    const { fixture, el } = await setup({ inYardVisits: () => Promise.resolve(inYard([row])), gateOut });
    expect(el.querySelector('tbody')?.textContent).toContain('CSQU3054383');

    [...el.querySelectorAll('button')].find((b) => b.textContent === 'Gate out')!.dispatchEvent(new Event('click'));
    await fixture.whenStable();
    const truck = el.querySelector<HTMLInputElement>('#out-truck-7')!;
    truck.value = 'DHK-TA-11-9999';
    truck.dispatchEvent(new Event('input'));
    el.querySelector<HTMLFormElement>('.out-form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(gateOut).toHaveBeenCalledWith(7, { truckNumber: 'DHK-TA-11-9999', damageNotes: null });
    expect(el.querySelector('.alert.success')?.textContent).toContain('INV-2026-0000001');
    expect(el.querySelector('.alert.success')?.textContent).toContain('2,000.00');
  });
});
