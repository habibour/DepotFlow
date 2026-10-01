using DepotFlow.Domain;

namespace DepotFlow.Seeder;

/// <summary>One visit as generated: compact on purpose, 1.5 million of them live in memory at once.</summary>
internal struct VisitRecord
{
    public int ContainerId;
    public int ShippingLineId;
    public long GateInTicks;
    public long GateOutTicks;   // 0 while the visit is still in the yard
    public int SlotId;          // 0 when not in the yard
}

internal sealed record SlotInfo(int Id, string Block, int Row, int Bay, int Tier);

internal sealed class GeneratedData
{
    public required string[] ContainerNumbers { get; init; }
    public required byte[] ContainerSizes { get; init; }
    public required VisitRecord[] Visits { get; init; }   // sorted by gate-in; visit id = index + 1
    public required DateTime AsOfUtc { get; init; }
    public required int InYardCount { get; init; }
}

/// <summary>
/// Generates the whole dataset in memory from one seed, so the same seed (and --as-of date) always gives the same data.
/// Rules (spec S3-03): about visits/4.3 containers with valid ISO 6346 numbers and a fixed size (60% 20 ft);
/// visits spread over 36 months with weekday/weekend variation and a right-skewed dwell time (mean about 8 days,
/// 1 to 60); a container never has overlapping visits; about 1,200 visits still in the yard, in valid stacks.
/// </summary>
internal static class DataGenerator
{
    private const double VisitsPerContainer = 4.3;
    private const double DwellMeanDays = 8.0;
    private const double DwellSigma = 0.9;          // log-normal spread: right-skewed
    private const double DwellMaxDays = 58.9;       // keeps the calendar dwell at 60 at most
    private const double DwellMinDays = 0.1;
    private static readonly TimeSpan DhakaOffset = TimeSpan.FromHours(6);
    private static readonly TimeSpan MinGapBetweenVisits = TimeSpan.FromHours(12);

    // Realistic owner prefixes; with the serial number they make every container number unique.
    private static readonly string[] OwnerCodes =
    [
        "MSK", "MSC", "CMA", "ONE", "HLX", "EGH", "COS", "HMM", "OOL", "YML", "ZIM", "PIL", "ANL", "WHL", "MAT",
        "ARK", "SIN", "XPR", "SWR", "CNC", "TWL", "SAF", "SEA", "SIT", "GLD", "APL", "TEM", "CAI", "TGH", "BMO"
    ];

    // Share of gate-ins per local hour of day: busy 07:00-19:00, quiet at night.
    private static readonly double[] HourWeights =
        [1, 1, 1, 1, 1, 2, 4, 8, 10, 10, 10, 10, 9, 10, 10, 10, 9, 8, 6, 4, 3, 2, 1, 1];

    public static GeneratedData Generate(
        SeederOptions options, IReadOnlyList<int> shippingLineIds, IReadOnlyList<SlotInfo> slots, Action<string> log)
    {
        var rng = new Random(options.Seed);
        var asOfUtc = options.AsOf.ToDateTime(TimeOnly.MinValue);   // midnight UTC at the start of the as-of day
        var windowStartUtc = asOfUtc.AddMonths(-36);

        var containerCount = Math.Max(1, (int)Math.Round(options.Visits / VisitsPerContainer));
        var inYardTarget = Math.Min(slots.Count, Math.Max(10, options.Visits / 1250));   // 1,200 at 1.5M visits

        // ---- containers -------------------------------------------------------------------------------------
        var numbers = new string[containerCount];
        var sizes = new byte[containerCount];
        for (var c = 0; c < containerCount; c++)
        {
            var first10 = $"{OwnerCodes[c % OwnerCodes.Length]}U{c / OwnerCodes.Length:D6}";
            numbers[c] = first10 + ContainerNumber.ComputeCheckDigit(first10);
            sizes[c] = rng.NextDouble() < 0.6 ? (byte)20 : (byte)40;
        }

        // ---- calendar weights: fewer gate-ins on Friday (Bangladesh's weekend) and Saturday ---------------------
        var dayCount = (int)(asOfUtc - windowStartUtc).TotalDays;
        var cumulative = new double[dayCount];
        var running = 0.0;
        for (var d = 0; d < dayCount; d++)
        {
            running += windowStartUtc.AddDays(d).DayOfWeek switch
            {
                DayOfWeek.Friday => 0.5,
                DayOfWeek.Saturday => 0.85,
                _ => 1.0
            };
            cumulative[d] = running;
        }

        var hourCumulative = HourWeights.Select((_, i) => HourWeights.Take(i + 1).Sum()).ToArray();

        long SampleGateIn(int fromDay, int toDay)
        {
            // Pick a day by weight inside [fromDay, toDay], then a time of day by weight.
            var lo = fromDay == 0 ? 0.0 : cumulative[fromDay - 1];
            var u = lo + rng.NextDouble() * (cumulative[toDay] - lo);
            var day = Math.Clamp(Array.BinarySearch(cumulative, u) is var i && i >= 0 ? i : ~i, fromDay, toDay);

            var h = rng.NextDouble() * hourCumulative[^1];
            var hour = Array.BinarySearch(hourCumulative, h) is var j && j >= 0 ? j : ~j;
            var local = windowStartUtc.AddDays(day).AddHours(hour).AddMinutes(rng.Next(60)).AddSeconds(rng.Next(60));
            return (local - DhakaOffset).Ticks;   // local Dhaka time -> UTC
        }

        // ---- which containers end the run still in the yard --------------------------------------------------
        var order = Enumerable.Range(0, containerCount).ToArray();
        for (var i = 0; i < inYardTarget; i++)   // partial Fisher-Yates: the first inYardTarget entries are a random sample
        {
            var j = i + rng.Next(containerCount - i);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var inYardContainer = new bool[containerCount];
        for (var i = 0; i < inYardTarget; i++)
        {
            inYardContainer[order[i]] = true;
        }

        // ---- shipping lines, weighted so a few big lines carry most of the traffic ----------------------------
        var lineCumulative = new double[shippingLineIds.Count];
        for (var i = 0; i < lineCumulative.Length; i++)
        {
            lineCumulative[i] = (i == 0 ? 0 : lineCumulative[i - 1]) + 1.0 / Math.Pow(i + 1, 0.8);
        }

        int SampleLine()
        {
            var u = rng.NextDouble() * lineCumulative[^1];
            var idx = Array.BinarySearch(lineCumulative, u) is var i && i >= 0 ? i : ~i;
            return shippingLineIds[Math.Min(idx, shippingLineIds.Count - 1)];
        }

        var mu = Math.Log(DwellMeanDays) - DwellSigma * DwellSigma / 2;   // so the mean dwell is about 8 days

        // ---- visits per container, without overlaps ------------------------------------------------------------
        var visits = new List<VisitRecord>((int)(options.Visits * 1.02));
        var ins = new long[64];
        var lastNormalDay = dayCount - 2;   // normal containers: last gate-in at least a day before the as-of date
        var yardDays = 14;

        for (var c = 0; c < containerCount; c++)
        {
            var k = Math.Min(1 + Poisson(rng, VisitsPerContainer - 1), ins.Length);   // mean 4.3 visits
            var stays = inYardContainer[c];

            // Gate-in instants. A container that ends the run in the yard has one final visit in the last two weeks;
            // all its earlier visits come before that.
            var earlier = stays ? k - 1 : k;
            for (var j = 0; j < earlier; j++)
            {
                ins[j] = SampleGateIn(0, stays ? dayCount - yardDays - 2 : lastNormalDay);
            }

            Array.Sort(ins, 0, earlier);
            for (var j = 1; j < earlier; j++)
            {
                ins[j] = Math.Max(ins[j], ins[j - 1] + MinGapBetweenVisits.Ticks);   // keep visits apart
            }

            int count;
            if (stays)
            {
                var final = SampleGateIn(dayCount - yardDays, dayCount - 1);
                var usable = earlier;
                // Pushing visits apart may have moved some too late to finish before the final visit starts: drop those.
                while (usable > 0 && ins[usable - 1] + MinGapBetweenVisits.Ticks > final)
                {
                    usable--;
                }

                ins[usable] = final;
                count = usable + 1;
            }
            else
            {
                count = earlier;
                var hardLimit = asOfUtc.Ticks - TimeSpan.FromHours(6).Ticks;
                while (count > 0 && ins[count - 1] > hardLimit)
                {
                    count--;   // pushing visits apart moved the tail past the end of the run: drop it
                }
            }

            for (var j = 0; j < count; j++)
            {
                var isFinalInYard = stays && j == count - 1;
                long outTicks = 0;

                if (!isFinalInYard)
                {
                    var maxOut = j < count - 1 ? ins[j + 1] - TimeSpan.FromHours(1).Ticks : asOfUtc.Ticks;
                    var dwellDays = Math.Clamp(Math.Exp(mu + DwellSigma * Normal(rng)), DwellMinDays, DwellMaxDays);
                    outTicks = Math.Min(ins[j] + (long)(dwellDays * TimeSpan.TicksPerDay), maxOut);
                }

                visits.Add(new VisitRecord
                {
                    ContainerId = c + 1,
                    ShippingLineId = SampleLine(),
                    GateInTicks = ins[j],
                    GateOutTicks = outTicks
                });
            }
        }

        var all = visits.ToArray();
        Array.Sort(all, static (a, b) => a.GateInTicks.CompareTo(b.GateInTicks));   // ids follow time
        log($"  generated {all.Length:N0} visits for {containerCount:N0} containers");

        var inYardCount = AssignSlots(all, slots, rng);
        log($"  {inYardCount:N0} visits are still in the yard, placed in valid stacks");

        return new GeneratedData
        {
            ContainerNumbers = numbers,
            ContainerSizes = sizes,
            Visits = all,
            AsOfUtc = asOfUtc,
            InYardCount = inYardCount
        };
    }

    /// <summary>
    /// Places every in-yard visit in a slot. Stacks (block, row, bay) are filled from the ground up in random order,
    /// so a container is only ever put on tier N when tier N-1 is occupied (the stacking rule), and no slot is used twice.
    /// </summary>
    private static int AssignSlots(VisitRecord[] visits, IReadOnlyList<SlotInfo> slots, Random rng)
    {
        var stacks = slots
            .GroupBy(s => (s.Block, s.Row, s.Bay))
            .Select(g => g.OrderBy(s => s.Tier).ToArray())
            .ToList();
        var filled = new int[stacks.Count];

        var count = 0;
        var remaining = visits.Count(v => v.GateOutTicks == 0);
        var free = stacks.Sum(s => s.Length);
        if (remaining > free)
        {
            throw new InvalidOperationException($"{remaining} visits are in the yard but there are only {free} slots.");
        }

        for (var i = 0; i < visits.Length; i++)
        {
            if (visits[i].GateOutTicks != 0)
            {
                continue;
            }

            int s;
            do
            {
                s = rng.Next(stacks.Count);
            }
            while (filled[s] == stacks[s].Length);

            visits[i].SlotId = stacks[s][filled[s]].Id;   // next free tier, counting up from tier 1
            filled[s]++;
            count++;
        }

        return count;
    }

    private static int Poisson(Random rng, double lambda)
    {
        var limit = Math.Exp(-lambda);
        var product = 1.0;
        var k = 0;
        do
        {
            k++;
            product *= rng.NextDouble();
        }
        while (product > limit);

        return k - 1;
    }

    private static double Normal(Random rng)   // Box-Muller
    {
        var u1 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * rng.NextDouble());
    }
}
