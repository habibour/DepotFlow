// Shapes of the API's JSON (camelCase, enums as strings). They mirror the backend DTOs; the backend is the authority.

export type Role = 'Admin' | 'GateClerk' | 'YardPlanner' | 'BillingOfficer';

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  role: Role;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ShippingLine {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
}

export interface GateInRequest {
  containerNumber: string;
  sizeFeet: number;
  shippingLineId: number;
  truckNumber: string;
  sealNumber: string;
  damageNotes: string | null;
}

export interface GateOutRequest {
  truckNumber: string;
  damageNotes: string | null;
}

export type VisitStatus = 'InYard' | 'Released';
export type InvoiceStatus = 'Issued' | 'Paid';

export interface InvoiceSummary {
  id: number;
  invoiceNumber: string;
  total: number;
  currency: string;
  status: InvoiceStatus;
}

/** The response of gate-in, gate-out and relocate. */
export interface Visit {
  id: number;
  containerId: number;
  containerNumber: string;
  sizeFeet: number;
  shippingLine: { id: number; code: string; name: string };
  yardSlot: { id: number; code: string } | null;
  status: VisitStatus;
  gateInAtUtc: string;
  gateOutAtUtc: string | null;
  truckInNumber: string;
  truckOutNumber: string | null;
  sealNumber: string;
  dwellDays: number | null;
  invoice: InvoiceSummary | null;
}

export interface VisitListItem {
  id: number;
  containerId: number;
  containerNumber: string;
  sizeFeet: number;
  shippingLineCode: string;
  slotCode: string | null;
  status: VisitStatus;
  gateInAtUtc: string;
  gateOutAtUtc: string | null;
  dwellDays: number | null;
}

export interface InvoiceListItem {
  id: number;
  invoiceNumber: string;
  visitId: number;
  containerNumber: string;
  shippingLineCode: string;
  issuedAtUtc: string;
  total: number;
  currency: string;
  status: InvoiceStatus;
}

export interface InvoiceLine {
  description: string;
  fromDay: number;
  toDay: number;
  days: number;
  ratePerDay: number;
  amount: number;
}

export interface InvoiceDetail extends InvoiceListItem {
  sizeFeet: number;
  gateInAtUtc: string;
  gateOutAtUtc: string | null;
  dwellDays: number;
  freeDays: number;
  strategyKey: string;
  paidAtUtc: string | null;
  paidByUserId: string | null;
  lines: InvoiceLine[];
}

export interface YardSlot {
  id: number;
  code: string;
  block: string;
  row: number;
  bay: number;
  tier: number;
  occupant: { visitId: number; containerNumber: string; sizeFeet: number } | null;
}

export interface YardOccupancyRow {
  block: string;
  totalSlots: number;
  occupiedSlots: number;
  occupiedTeu: number;
  percentOccupied: number;
}

export interface YardOccupancyReport {
  rows: YardOccupancyRow[];
  total: YardOccupancyRow;
}

export interface DailyMovementsReport {
  from: string;
  to: string;
  rows: { date: string; gateIns: number; gateOuts: number }[];
  total: { gateIns: number; gateOuts: number };
}

export interface DwellTimeReport {
  from: string;
  to: string;
  rows: { shippingLineId: number; code: string; releasedVisits: number; avgDwellDays: number; medianDwellDays: number; maxDwellDays: number }[];
}

export interface RevenueRow {
  invoices: number;
  totalBilled: number;
  totalPaid: number;
  outstanding: number;
}

export interface RevenueReport {
  from: string;
  to: string;
  currency: string;
  rows: (RevenueRow & { shippingLineId: number; code: string })[];
  total: RevenueRow;
}

/** An RFC 7807 problem response, with the API's machine-readable "code" and the trace id. */
export interface Problem {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}
