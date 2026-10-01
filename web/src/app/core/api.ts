import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  DailyMovementsReport,
  DwellTimeReport,
  GateInRequest,
  GateOutRequest,
  InvoiceDetail,
  InvoiceListItem,
  InvoiceStatus,
  Paged,
  RevenueReport,
  ShippingLine,
  Visit,
  VisitListItem,
  YardOccupancyReport,
  YardSlot,
} from './api.types';

/** One method per API call the screens need. Errors are left as HttpErrorResponse for describeError() to explain. */
@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1';

  shippingLines(): Promise<Paged<ShippingLine>> {
    return firstValueFrom(this.http.get<Paged<ShippingLine>>(`${this.base}/shipping-lines`, { params: { isActive: true, pageSize: 100 } }));
  }

  gateIn(request: GateInRequest): Promise<Visit> {
    return firstValueFrom(this.http.post<Visit>(`${this.base}/visits/gate-in`, request));
  }

  gateOut(visitId: number, request: GateOutRequest): Promise<Visit> {
    return firstValueFrom(this.http.post<Visit>(`${this.base}/visits/${visitId}/gate-out`, request));
  }

  inYardVisits(page = 1, pageSize = 50): Promise<Paged<VisitListItem>> {
    return firstValueFrom(
      this.http.get<Paged<VisitListItem>>(`${this.base}/visits`, { params: { status: 'InYard', sort: '-gateInAtUtc', page, pageSize } }),
    );
  }

  invoices(status: InvoiceStatus | '', page: number, pageSize = 15): Promise<Paged<InvoiceListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) {
      params = params.set('status', status);
    }
    return firstValueFrom(this.http.get<Paged<InvoiceListItem>>(`${this.base}/invoices`, { params }));
  }

  invoice(id: number): Promise<InvoiceDetail> {
    return firstValueFrom(this.http.get<InvoiceDetail>(`${this.base}/invoices/${id}`));
  }

  payInvoice(id: number): Promise<InvoiceDetail> {
    return firstValueFrom(this.http.post<InvoiceDetail>(`${this.base}/invoices/${id}/pay`, {}));
  }

  yardOccupancy(): Promise<YardOccupancyReport> {
    return firstValueFrom(this.http.get<YardOccupancyReport>(`${this.base}/reports/yard-occupancy`));
  }

  /** All slots of one block (the endpoint pages at most 200 at a time). */
  async yardBlock(block: string): Promise<YardSlot[]> {
    const slots: YardSlot[] = [];
    for (let page = 1; ; page++) {
      const result = await firstValueFrom(
        this.http.get<Paged<YardSlot>>(`${this.base}/yard/slots`, { params: { block, page, pageSize: 200 } }),
      );
      slots.push(...result.items);
      if (slots.length >= result.totalCount || result.items.length === 0) {
        return slots;
      }
    }
  }

  dailyMovements(from: string, to: string): Promise<DailyMovementsReport> {
    return firstValueFrom(this.http.get<DailyMovementsReport>(`${this.base}/reports/daily-movements`, { params: { from, to } }));
  }

  dwellTime(from: string, to: string): Promise<DwellTimeReport> {
    return firstValueFrom(this.http.get<DwellTimeReport>(`${this.base}/reports/dwell-time`, { params: { from, to } }));
  }

  revenue(from: string, to: string): Promise<RevenueReport> {
    return firstValueFrom(this.http.get<RevenueReport>(`${this.base}/reports/revenue`, { params: { from, to } }));
  }
}
