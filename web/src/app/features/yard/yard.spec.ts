import { YardSlot } from '../../core/api.types';
import { buildGrid } from './yard';

const slot = (id: number, row: number, bay: number, tier: number, occupied = false): YardSlot => ({
  id, code: `A-${row}-${bay}-${tier}`, block: 'A', row, bay, tier,
  occupant: occupied ? { visitId: id, containerNumber: 'CSQU3054383', sizeFeet: 20 } : null,
});

describe('buildGrid', () => {
  it('groups slots into stacks by row and bay, highest tier first', () => {
    const grid = buildGrid([slot(1, 1, 1, 1), slot(2, 1, 1, 2), slot(3, 1, 2, 1), slot(4, 2, 1, 1)]);

    expect(grid.bays).toEqual([1, 2]);
    expect(grid.rows.map((r) => r.row)).toEqual([1, 2]);
    expect(grid.rows[0].stacks[0]?.tiers.map((t) => t.tier)).toEqual([2, 1]);
    expect(grid.rows[0].stacks[1]?.tiers.map((t) => t.tier)).toEqual([1]);
  });

  it('leaves a gap where a row has no stack in that bay', () => {
    const grid = buildGrid([slot(1, 1, 1, 1), slot(2, 2, 2, 1)]);
    expect(grid.rows[0].stacks).toEqual([expect.anything(), null]);
    expect(grid.rows[1].stacks).toEqual([null, expect.anything()]);
  });

  it('is empty for no slots', () => {
    expect(buildGrid([])).toEqual({ bays: [], rows: [] });
  });
});
