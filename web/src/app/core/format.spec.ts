import { formatDate, formatDateTime, formatMoney, toIsoDate } from './format';

describe('formatting', () => {
  it('shows UTC times as Dhaka local time (UTC+6)', () => {
    expect(formatDateTime('2026-10-01T17:50:00Z')).toBe('1 Oct 2026, 23:50');
    expect(formatDateTime('2026-10-01T18:10:00Z')).toBe('2 Oct 2026, 00:10');   // already the next day in Dhaka
  });

  it('formats dates and empty values', () => {
    expect(formatDate('2026-10-01T18:10:00Z')).toBe('2 Oct 2026');
    expect(formatDateTime(null)).toBe('-');
    expect(formatDate(null)).toBe('-');
  });

  it('uses the Dhaka calendar date for report ranges', () => {
    expect(toIsoDate(new Date('2026-10-01T18:10:00Z'))).toBe('2026-10-02');
    expect(toIsoDate(new Date('2026-10-01T17:50:00Z'))).toBe('2026-10-01');
  });

  it('formats money with the currency code', () => {
    expect(formatMoney(2000, 'BDT')).toContain('2,000.00');
    expect(formatMoney(2000, 'BDT')).toContain('BDT');
  });
});
