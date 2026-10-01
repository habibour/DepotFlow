using DepotFlow.Domain;
using DepotFlow.Seeder;

namespace DepotFlow.Api.Tests;

// Covers spec S3-03 (the generator's rules) without a database: fast, so they run on every build.
public class SeederGeneratorTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 1);

    // The default yard: 4 blocks x 10 rows x 10 bays x 4 tiers = 1,600 slots.
    private static readonly List<SlotInfo> Slots =
        (from block in new[] { "A", "B", "C", "D" }
         from row in Enumerable.Range(1, 10)
         from bay in Enumerable.Range(1, 10)
         from tier in Enumerable.Range(1, 4)
         select (block, row, bay, tier))
        .Select((s, i) => new SlotInfo(i + 1, s.block, s.row, s.bay, s.tier))
        .ToList();

    private static GeneratedData Generate(int visits = 30_000, int seed = 42) =>
        DataGenerator.Generate(
            new SeederOptions(visits, seed, false, false, "DepotFlowBench", null, AsOf),
            Enumerable.Range(1, 25).ToList(), Slots, _ => { });

    [Fact]
    public void The_same_seed_gives_identical_data_and_a_different_seed_does_not()
    {
        var a = Generate(seed: 42);
        var b = Generate(seed: 42);
        var c = Generate(seed: 43);

        Assert.Equal(a.ContainerNumbers, b.ContainerNumbers);
        Assert.Equal(a.Visits.Select(v => (v.ContainerId, v.ShippingLineId, v.GateInTicks, v.GateOutTicks, v.SlotId)),
                     b.Visits.Select(v => (v.ContainerId, v.ShippingLineId, v.GateInTicks, v.GateOutTicks, v.SlotId)));
        Assert.NotEqual(a.Visits.Select(v => v.GateInTicks), c.Visits.Select(v => v.GateInTicks));
    }

    [Fact]
    public void The_visit_count_is_within_one_percent_and_visits_are_in_time_order()
    {
        var data = Generate(visits: 50_000);

        Assert.InRange(data.Visits.Length, 49_500, 50_500);
        Assert.Equal(data.Visits.OrderBy(v => v.GateInTicks).Select(v => v.GateInTicks), data.Visits.Select(v => v.GateInTicks));
    }

    [Fact]
    public void Every_container_number_is_valid_and_unique_and_sizes_are_20_or_40()
    {
        var data = Generate();

        Assert.All(data.ContainerNumbers, n => Assert.True(ContainerNumber.TryCreate(n, out _, out var error), error));
        Assert.Equal(data.ContainerNumbers.Length, data.ContainerNumbers.Distinct().Count());
        Assert.All(data.ContainerSizes, s => Assert.Contains(s, new byte[] { 20, 40 }));
        var share20 = data.ContainerSizes.Count(s => s == 20) / (double)data.ContainerSizes.Length;
        Assert.InRange(share20, 0.57, 0.63);   // 60% are 20 ft
    }

    [Fact]
    public void A_container_never_has_overlapping_visits_and_only_its_last_visit_can_be_in_the_yard()
    {
        var data = Generate();

        foreach (var visits in data.Visits.GroupBy(v => v.ContainerId))
        {
            var ordered = visits.OrderBy(v => v.GateInTicks).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var visit = ordered[i];
                if (visit.GateOutTicks == 0)
                {
                    Assert.Equal(ordered.Count - 1, i);   // in the yard: must be the last
                    continue;
                }

                Assert.True(visit.GateOutTicks > visit.GateInTicks);
                Assert.True(visit.GateOutTicks <= data.AsOfUtc.Ticks);
                if (i + 1 < ordered.Count)
                {
                    Assert.True(visit.GateOutTicks < ordered[i + 1].GateInTicks, "visits of one container overlap");
                }
            }
        }
    }

    [Fact]
    public void In_yard_visits_are_about_one_per_1250_placed_in_unique_slots_that_obey_the_stacking_rule()
    {
        var data = Generate(visits: 50_000);

        var inYard = data.Visits.Where(v => v.GateOutTicks == 0).ToList();
        Assert.Equal(40, inYard.Count);   // 50,000 / 1,250
        Assert.Equal(inYard.Count, inYard.Select(v => v.SlotId).Distinct().Count());
        Assert.All(inYard, v => Assert.NotEqual(0, v.SlotId));
        Assert.All(data.Visits.Where(v => v.GateOutTicks != 0), v => Assert.Equal(0, v.SlotId));

        var occupied = inYard.Select(v => Slots.Single(s => s.Id == v.SlotId)).ToList();
        foreach (var slot in occupied.Where(s => s.Tier > 1))
        {
            Assert.Contains(occupied, o => (o.Block, o.Row, o.Bay, o.Tier) == (slot.Block, slot.Row, slot.Bay, slot.Tier - 1));
        }
    }

    [Fact]
    public void Dwell_time_is_right_skewed_with_a_mean_near_8_days_between_1_and_60()
    {
        var data = Generate(visits: 100_000);

        var dwell = data.Visits.Where(v => v.GateOutTicks != 0)
            .Select(v => BulkLoader.LocalDwellDays(v.GateInTicks, v.GateOutTicks)).ToList();

        Assert.InRange(dwell.Average(), 7.0, 9.5);
        Assert.InRange(dwell.Min(), 1, 1);
        Assert.InRange(dwell.Max(), 30, 60);
        var sorted = dwell.Order().ToList();
        Assert.True(sorted[sorted.Count / 2] < dwell.Average(), "median should be below the mean (right-skewed)");
    }

    [Fact]
    public void Gate_ins_span_36_months_and_fridays_are_quieter_than_weekdays()
    {
        var data = Generate(visits: 100_000);

        var days = data.Visits.Select(v => new DateTime(v.GateInTicks).AddHours(6)).ToList();   // Dhaka local
        Assert.True((data.AsOfUtc - days.Min()).TotalDays > 36 * 30 - 30);

        double PerDay(DayOfWeek d) => days.Count(x => x.DayOfWeek == d) / (double)days.Select(x => x.Date).Distinct().Count(x => x.DayOfWeek == d);
        Assert.True(PerDay(DayOfWeek.Friday) < PerDay(DayOfWeek.Tuesday) * 0.8);
    }
}
