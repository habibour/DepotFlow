const DHAKA = 'Asia/Dhaka';

const dateTime = new Intl.DateTimeFormat('en-GB', { timeZone: DHAKA, dateStyle: 'medium', timeStyle: 'short' });
const dateOnly = new Intl.DateTimeFormat('en-GB', { timeZone: DHAKA, dateStyle: 'medium' });

/** The API stores UTC; people at the depot read Dhaka local time. */
export function formatDateTime(iso: string | null): string {
  return iso ? dateTime.format(new Date(iso)) : '-';
}

export function formatDate(iso: string | null): string {
  return iso ? dateOnly.format(new Date(iso)) : '-';
}

export function formatMoney(amount: number, currency = 'BDT'): string {
  return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
}

/** yyyy-MM-dd for a Date, in local Dhaka calendar terms, as the report endpoints expect. */
export function toIsoDate(date: Date): string {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: DHAKA, year: 'numeric', month: '2-digit', day: '2-digit' }).format(date);
  return parts;   // en-CA formats as yyyy-MM-dd
}
