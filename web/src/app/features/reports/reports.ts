import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../../core/api';
import { DailyMovementsReport, DwellTimeReport, RevenueReport, YardOccupancyReport } from '../../core/api.types';
import { AuthService } from '../../core/auth.service';
import { formatDate, formatMoney, toIsoDate } from '../../core/format';
import { ErrorMessage, describeError } from '../../core/problem';

@Component({
  selector: 'app-reports',
  imports: [FormsModule, DecimalPipe],
  templateUrl: './reports.html',
  styleUrl: './reports.css',
})
export class Reports implements OnInit {
  private readonly api = inject(Api);
  private readonly auth = inject(AuthService);

  protected readonly formatDate = formatDate;
  protected readonly formatMoney = formatMoney;

  // Which reports this role may call (the API enforces the same rules).
  protected readonly canYard = this.auth.hasRole('Admin', 'YardPlanner');
  protected readonly canDaily = this.auth.hasRole('Admin', 'YardPlanner');
  protected readonly canDwell = this.auth.hasRole('Admin', 'YardPlanner', 'BillingOfficer');
  protected readonly canRevenue = this.auth.hasRole('Admin', 'BillingOfficer');

  protected to = toIsoDate(new Date());
  protected from = toIsoDate(new Date(Date.now() - 29 * 86_400_000));

  protected readonly busy = signal(false);
  protected readonly error = signal<ErrorMessage | null>(null);
  protected readonly yard = signal<YardOccupancyReport | null>(null);
  protected readonly daily = signal<DailyMovementsReport | null>(null);
  protected readonly dwell = signal<DwellTimeReport | null>(null);
  protected readonly revenue = signal<RevenueReport | null>(null);

  async ngOnInit(): Promise<void> {
    await this.run();
  }

  protected barWidth(value: number): number {
    const max = Math.max(1, ...(this.daily()?.rows.flatMap((r) => [r.gateIns, r.gateOuts]) ?? [1]));
    return (value / max) * 100;
  }

  protected async run(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await Promise.all([
        this.canYard ? this.api.yardOccupancy().then((r) => this.yard.set(r)) : Promise.resolve(),
        this.canDaily ? this.api.dailyMovements(this.from, this.to).then((r) => this.daily.set(r)) : Promise.resolve(),
        this.canDwell ? this.api.dwellTime(this.from, this.to).then((r) => this.dwell.set(r)) : Promise.resolve(),
        this.canRevenue ? this.api.revenue(this.from, this.to).then((r) => this.revenue.set(r)) : Promise.resolve(),
      ]);
    } catch (error) {
      this.error.set(describeError(error));
    } finally {
      this.busy.set(false);
    }
  }
}
