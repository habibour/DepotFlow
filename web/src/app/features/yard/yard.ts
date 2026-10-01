import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Api } from '../../core/api';
import { YardOccupancyRow, YardSlot } from '../../core/api.types';
import { ErrorMessage, describeError } from '../../core/problem';

export interface Stack {
  row: number;
  bay: number;
  /** Highest tier first, so the stack is drawn the way it stands. */
  tiers: YardSlot[];
}

/** Groups a block's slots into stacks (row, bay) laid out as rows of bays. Pure, so it is easy to test. */
export function buildGrid(slots: YardSlot[]): { bays: number[]; rows: { row: number; stacks: (Stack | null)[] }[] } {
  const bays = [...new Set(slots.map((s) => s.bay))].sort((a, b) => a - b);
  const rowNumbers = [...new Set(slots.map((s) => s.row))].sort((a, b) => a - b);

  const byPosition = new Map<string, YardSlot[]>();
  for (const slot of slots) {
    const key = `${slot.row}-${slot.bay}`;
    byPosition.set(key, [...(byPosition.get(key) ?? []), slot]);
  }

  return {
    bays,
    rows: rowNumbers.map((row) => ({
      row,
      stacks: bays.map((bay) => {
        const tiers = byPosition.get(`${row}-${bay}`);
        return tiers ? { row, bay, tiers: [...tiers].sort((a, b) => b.tier - a.tier) } : null;
      }),
    })),
  };
}

@Component({
  selector: 'app-yard',
  templateUrl: './yard.html',
  styleUrl: './yard.css',
})
export class Yard implements OnInit {
  private readonly api = inject(Api);

  protected readonly blocks = signal<YardOccupancyRow[]>([]);
  protected readonly block = signal('');
  protected readonly slots = signal<YardSlot[]>([]);
  protected readonly selected = signal<YardSlot | null>(null);
  protected readonly error = signal<ErrorMessage | null>(null);
  protected readonly loading = signal(false);

  protected readonly grid = computed(() => buildGrid(this.slots()));
  protected readonly summary = computed(() => this.blocks().find((b) => b.block === this.block()) ?? null);

  async ngOnInit(): Promise<void> {
    try {
      const occupancy = await this.api.yardOccupancy();
      this.blocks.set(occupancy.rows);
      if (occupancy.rows.length > 0) {
        await this.selectBlock(occupancy.rows[0].block);
      }
    } catch (error) {
      this.error.set(describeError(error));
    }
  }

  protected async selectBlock(block: string): Promise<void> {
    this.block.set(block);
    this.selected.set(null);
    this.loading.set(true);
    this.error.set(null);
    try {
      this.slots.set(await this.api.yardBlock(block));
    } catch (error) {
      this.error.set(describeError(error));
    } finally {
      this.loading.set(false);
    }
  }

  protected select(slot: YardSlot): void {
    this.selected.set(slot.occupant ? slot : null);
  }
}
