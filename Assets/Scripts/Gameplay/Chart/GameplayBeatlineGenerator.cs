using System.Collections.Generic;
using ArcCreate.Gameplay.Data;
using UnityEngine;

namespace ArcCreate.Gameplay.Chart
{
    public class GameplayBeatlineGenerator : IBeatlineGenerator
    {
        private readonly Color beatlineColor;

        public GameplayBeatlineGenerator(Color beatlineColor)
        {
            this.beatlineColor = beatlineColor;
        }

        public IEnumerable<Beatline> Generate(TimingGroup tg, int audioLength)
        {
            List<TimingEvent> timings = tg.Timings;

            // Extend before timing = 0;
            {
                TimingEvent firstTiming = timings[0];

                float start = -3000 - Values.ChartAudioOffset - Settings.GlobalAudioOffset.Value;

                if (firstTiming.Bpm <= 10000)
                {
                    float distanceBetweenTwoLine =
                        firstTiming.Bpm * firstTiming.Divisor == 0 ?
                        float.MaxValue :
                        60000f / Mathf.Abs(firstTiming.Bpm) * firstTiming.Divisor;
                    distanceBetweenTwoLine = Mathf.Max(distanceBetweenTwoLine, 1);

                    if (distanceBetweenTwoLine > 0)
                    {
                        for (float timing = 0; timing >= start; timing -= distanceBetweenTwoLine)
                        {
                            yield return new Beatline(
                                Mathf.RoundToInt(timing),
                                tg.GetFloorPosition(Mathf.RoundToInt(timing)),
                                Values.BeatlineThickness,
                                beatlineColor);
                        }
                    }
                }
            }

            for (int i = 0; i < timings.Count - 1; i++)
            {
                TimingEvent currentTiming = timings[i];
                int limit = timings[i + 1].Timing;

                if (currentTiming.Bpm > 10000)
                {
                    continue;
                }

                float distanceBetweenTwoLine =
                    currentTiming.Bpm * currentTiming.Divisor == 0 ?
                    float.MaxValue :
                    60000f / Mathf.Abs(currentTiming.Bpm) * currentTiming.Divisor;
                distanceBetweenTwoLine = Mathf.Max(distanceBetweenTwoLine, 1);

                if (distanceBetweenTwoLine <= 0)
                {
                    continue;
                }

                for (float timing = currentTiming.Timing; timing < limit; timing += distanceBetweenTwoLine)
                {
                    yield return new Beatline(
                        Mathf.RoundToInt(timing),
                        tg.GetFloorPosition(Mathf.RoundToInt(timing)),
                        Values.BeatlineThickness,
                        beatlineColor);
                }
            }

            // Last timing event extend until end of audio
            {
                TimingEvent lastTiming = timings[timings.Count - 1];
                int limit = audioLength;

                if (lastTiming.Bpm <= 10000)
                {
                    float distanceBetweenTwoLine =
                        lastTiming.Bpm * lastTiming.Divisor == 0 ?
                        float.MaxValue :
                        60000f / Mathf.Abs(lastTiming.Bpm) * lastTiming.Divisor;
                    distanceBetweenTwoLine = Mathf.Max(distanceBetweenTwoLine, 1);

                    if (distanceBetweenTwoLine > 0)
                    {
                        for (float timing = lastTiming.Timing; timing <= limit; timing += distanceBetweenTwoLine)
                        {
                            yield return new Beatline(
                                Mathf.RoundToInt(timing),
                                tg.GetFloorPosition(Mathf.RoundToInt(timing)),
                                Values.BeatlineThickness,
                                beatlineColor);
                        }
                    }
                }
            }
        }
    }
}