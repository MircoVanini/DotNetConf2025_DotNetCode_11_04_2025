using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Numerics;

BenchmarkRunner.Run<PerformanceTest>();

[RankColumn]
[MemoryDiagnoser(false)]
[HideColumns("Job", "Error", "StdDev", "Median", "RatioSD")]
public class PerformanceTest
{
    float [] data = new float[10_240_000];

    [GlobalSetup]
    public void Setup()
    {
        for(int i = 0; i < data.Length; i++)
        {
            data[i] = i;
        }
    }

    [Benchmark(Baseline = true)]
    public void Normalize()
    {                
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = data[i] / 2f;
        }
    }

    [Benchmark]
    public void NormalizeWithSIMD()
    {
        Vector<float> factor = new Vector<float>(0.5f);
        for (int i = 0; i < data.Length; i += Vector<float>.Count)
        {
            Vector<float> vector = new Vector<float>(data, i);
            (vector * factor).CopyTo(data, i); 
        }
    }
}