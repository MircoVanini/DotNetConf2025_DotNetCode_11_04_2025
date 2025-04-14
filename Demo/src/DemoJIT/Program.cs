using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using System.IO.Hashing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Text;


[RankColumn]
[MemoryDiagnoser(false)]
[DisassemblyDiagnoser(maxDepth: 0)]
[HideColumns("Job", "Error", "StdDev", "Median", "RatioSD")]
public partial class Program
{
    static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .AddJob(Job.Default.WithId(".NET 8").WithRuntime(CoreRuntime.Core80).WithEnvironmentVariable("DOTNET_ReadyToRun", "0"))
            .AddJob(Job.Default.WithId(".NET 9").WithRuntime(CoreRuntime.Core90).WithEnvironmentVariable("DOTNET_ReadyToRun", "0"));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    /////////////////////////////////////
    private byte[] _dataToHash = new byte[1024 * 1024];

    [GlobalSetup]
    public void Setup()
    {
        new Random(42).NextBytes(_dataToHash);
    }

    /////////////////////////////////////

    public class A { }
    public class B : A { }
    public class C : B { }

    private A _obj = new C();

    [Benchmark]
    public bool DemoCast() => _obj is B;

    /////////////////////////////////////

    [Benchmark]
    [Arguments("abcd", "abcg")]
    public bool DemoEquals(string a, string b) => a == b;

    static int[] _dataLeft  = [1,2,3,4,5,6,7,8,9,0,9,8,7,6,5,4,3,2,1];
    static int[] _dataRight = [1,2,3,4,5,6,7,8,9,0,9,8,7,6,5,4,3,2,1];

    [Benchmark]   
    public bool DemoSequenceEquals() => _dataLeft.AsSpan().SequenceEqual(_dataRight);

    /////////////////////////////////////    

    [Benchmark]
    public int DemoLoop()
    {
        int sum = 0;
        for (int i = 0; i < 1024; i++)
        {
            sum += i;
        }

        return sum;
    }

    /////////////////////////////////////

    [Benchmark]
    public void DemoT0()
    {
        for (int i = 0; i < 1024; i++)
            ThrowIfNull(i);
    }
	private void ThrowIfNull<T>(T a)
	{
		ArgumentNullException.ThrowIfNull(a);
	}

    /////////////////////////////////////

    [Benchmark]
    [Arguments(3)]
    public int DemoBoundsChecks() => Calculate(0, "1234567890abcdefghijklmnopqrstuvwxyz");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Calculate(int i, ReadOnlySpan<char> src)
    {
        int sum = 0;

        for (; (uint)i < src.Length; i++)
        {
            sum += src[i];
        }

        return sum;
    }

    private readonly string[] _names = Enum.GetNames<MyEnum>();
	public enum MyEnum : ulong { A, B, C, D }
	
    [Benchmark]
    [Arguments(2)]
    public string? DemoBoundsChecks2(ulong ulValue)
    {
        string[] names = _names;
        string? ret = null;

        for(int i = 0; i < 1024; i++)
        {
            ret = ulValue < (ulong)names.Length ?
                            names[(uint)ulValue] :
                            null;
        }

        return ret;
    }

    /////////////////////////////////////

    [Benchmark]
    public void DemoWriteBarrier()
    {
        MyRefStruct s = default;
        BarrierTest(ref s, new object(), new object());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BarrierTest(ref MyRefStruct s, object o1, object o2)
    {
        s.Obj1 = o1;
        s.Obj2 = o2;
    }
    private ref struct MyRefStruct
    {
        public object Obj1;
        public object Obj2;
    }

    /////////////////////////////////////

    [Benchmark]    
    public int DemoObjectStackAllocation() => new MyObj(42).Value;

    private class MyObj
    {
        public MyObj(int value) => Value = value;
        public int Value { get; }
    }

    /////////////////////////////////////

    [Benchmark]
    public string DemoSpan() => Path.Join("a", "b", "c", "d", "e");

    [Benchmark]
    [Arguments("helloworld.txt")]
    public bool DemoSpan2(string path) => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    /////////////////////B////////////////

    [Benchmark]
    public int DemoObjectStackAllocationForBoxes()
    {
        bool result = Compare(3, 4);
        return result ? 0 : 100;
    }

    bool Compare(object? x, object? y)
    {
        if ((x == null) || (y == null))
        {
            return x == y;
        }

        return x.Equals(y);
    }

    /////////////////////////////////////

    [Benchmark]
    public void DemoVector512()
    {
        Exp512(Vector512.Create((byte)1),
               Vector512.Create((byte)2));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector512<byte> Exp512(Vector512<byte> b, Vector512<byte> c) =>
        Vector512.ConditionalSelect(Vector512.LessThan(b, c), b + c, b - c);

    [Benchmark]
    public void DemoVector512Bis() => Vector512.Create("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"u8);


    /////////////////////////////////////

    private Vector128<byte> _v1 = Vector128.Create((byte)0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);

    [Benchmark]
    public Vector128<byte> DemoVectorSquare() => _v1 * _v1;

    [Benchmark]
    public UInt128 DemoVectorHash() => XxHash128.HashToUInt128(_dataToHash);

    /////////////////////////////////////

    private IEnumerable<int> _arrayDistinct = Enumerable.Range(0, 1000).ToArray().Distinct();
    private IEnumerable<int> _appendSelect = Enumerable.Range(0, 1000).ToArray().Append(42).Select(i => i * 2);
    private IEnumerable<int> _rangeReverse = Enumerable.Range(0, 1000).Reverse();
    private IEnumerable<int> _listDefaultIfEmptySelect = Enumerable.Range(0, 1000).ToList().DefaultIfEmpty().Select(i => i * 2);
    private IEnumerable<int> _listSkipTake = Enumerable.Range(0, 1000).ToList().Skip(500).Take(100);
    private IEnumerable<int> _rangeUnion = Enumerable.Range(0, 1000).Union(Enumerable.Range(500, 1000));

    [Benchmark] public int DemoDistinctFirst() => _arrayDistinct.First();
    [Benchmark] public int DemoAppendSelectLast() => _appendSelect.Last();
    [Benchmark] public int DemoRangeReverseCount() => _rangeReverse.Count();
    [Benchmark] public int DemoDefaultIfEmptySelectElementAt() => _listDefaultIfEmptySelect.ElementAt(999);
    [Benchmark] public int DemoListSkipTakeElementAt() => _listSkipTake.ElementAt(99);
    [Benchmark] public int DemoRangeUnionFirst() => _rangeUnion.First();

    private string[] _values = [];

    [Benchmark] public object DemoChunk() => _values.Chunk(10);
    [Benchmark] public object DemoDistinct() => _values.Distinct();
    [Benchmark] public object DemoGroupJoin() => _values.GroupJoin(_values, i => i, i => i, (i, j) => i);
    [Benchmark] public object DemoJoin() => _values.Join(_values, i => i, i => i, (i, j) => i);
    [Benchmark] public object DemoToLookup() => _values.ToLookup(i => i);
    [Benchmark] public object DemoReverse() => _values.Reverse();
    [Benchmark] public object DemoSelectIndex() => _values.Select((s, i) => i);
    [Benchmark] public object DemoSelectMany() => _values.SelectMany(i => i);
    [Benchmark] public object DemoSkipWhile() => _values.SkipWhile(i => true);
    [Benchmark] public object DemoTakeWhile() => _values.TakeWhile(i => true);
    [Benchmark] public object DemoWhereIndex() => _values.Where((s, i) => true);


    private void Normalize(float[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = data[i] / 2f;
        }
    }

    private void NormalizeWithSIMD(float[] data)
    {
        Vector<float> factor = new Vector<float>(0.5f);
        for (int i = 0; i < data.Length; i += Vector<float>.Count)
        {
            Vector<float> vector = new Vector<float>(data, i);
            (vector * factor).CopyTo(data, i); 
        }
    }
}