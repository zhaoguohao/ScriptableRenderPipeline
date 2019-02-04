using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Random = UnityEngine.Random;

namespace UnityEditor.Experimental.Rendering.Tests
{
    public unsafe class CoreUnsafeUtilsTests
    {
        public struct TestData : IEquatable<TestData>
        {
            public readonly int intValue;
            public readonly float floatValue;

            public bool Equals(TestData other) => intValue == other.intValue && floatValue == other.floatValue;

            public override bool Equals(object obj) => obj is TestData data && Equals(data);

            public override int GetHashCode() 
                => (int)((uint)intValue ^ BitConverter.ToUInt32(BitConverter.GetBytes(floatValue), 0));

            public TestData(int intValue, float floatValue)
            {
                this.intValue = intValue;
                this.floatValue = floatValue;
            }
        }

        static object[][] s_CopyToList = {
            new object[] { new List<TestData>
            {
                new TestData(2, 1),
                new TestData(3, 2),
                new TestData(4, 3),
                new TestData(5, 4),
                new TestData(6, 5),
            } }
        };

        [Test]
        [TestCaseSource(nameof(s_CopyToList))]
        public void CopyToList(List<TestData> datas)
        {
            var dest = stackalloc TestData[datas.Count];
            datas.CopyTo(dest, datas.Count);

            for (var i = 0; i < datas.Count; ++i)
                Assert.AreEqual(datas[i], dest[i]);
        }



        static object[][] s_CopyToArray = {
            new object[] { new[]
            {
                new TestData(2, 1),
                new TestData(3, 2),
                new TestData(4, 3),
                new TestData(5, 4),
                new TestData(6, 5),
            } }
        };

        [Test]
        [TestCaseSource(nameof(s_CopyToArray))]
        public void CopyToArray(TestData[] datas)
        {
            var dest = stackalloc TestData[datas.Length];
            datas.CopyTo(dest, datas.Length);

            for (var i = 0; i < datas.Length; ++i)
                Assert.AreEqual(datas[i], dest[i]);
        }

        static object[][] s_QuickSort = {
            new object[] { new[] { 0, 1 } },
            new object[] { new[] { 1, 0 } },
            new object[] { new[] { 0, 4, 2, 6, 3, 7, 1, 5 } }, // Test with unique set
            new object[] { new[] { 0, 4, 2, 6, 4, 7, 1, 5 } }, // Test with non unique set
        };

        [Test]
        [TestCaseSource(nameof(s_QuickSort))]
        public void QuickSort(int[] values)
        {
            // We must perform a copy to avoid messing the test data directly
            var ptrValues = stackalloc int[values.Length];
            values.CopyTo(ptrValues, values.Length);

            CoreUnsafeUtils.QuickSort<int>(values.Length, ptrValues);

            for (var i = 0; i< values.Length - 1; ++i)
                Assert.LessOrEqual(ptrValues[i], ptrValues[i + 1]);
        }

        static object[][] s_QuickSortHash = {
            new object[]
            {
                new[] { Hash128.Parse("78b27b84a9011b5403e836b9dfa51e33"), Hash128.Parse("c7417d322c083197631326bccf3f9ea0"), Hash128.Parse("dd27f0dc4ffe20b0f8ecc0e4fdf618fe") },
                new[] { Hash128.Parse("dd27f0dc4ffe20b0f8ecc0e4fdf618fe"), Hash128.Parse("c7417d322c083197631326bccf3f9ea0"), Hash128.Parse("78b27b84a9011b5403e836b9dfa51e33") },
            },
        };

        [Test]
        [TestCaseSource(nameof(s_QuickSortHash))]
        public void QuickSortHash(Hash128[] l, Hash128[] r)
        {
            var lPtr = stackalloc Hash128[l.Length];
            var rPtr = stackalloc Hash128[r.Length];
            for (var i = 0; i < l.Length; ++i)
            {
                lPtr[i] = l[i];
                rPtr[i] = r[i];
            }

            CoreUnsafeUtils.QuickSort<Hash128>(l.Length, lPtr);
            CoreUnsafeUtils.QuickSort<Hash128>(r.Length, rPtr);

            for (var i = 0; i < l.Length - 1; ++i)
            {
                Assert.LessOrEqual(lPtr[i], lPtr[i + 1]);
                Assert.LessOrEqual(rPtr[i], rPtr[i + 1]);
            }

            for (var i = 0; i < l.Length; ++i)
            {
                Assert.AreEqual(lPtr[i], rPtr[i]);
            }
        }

        [PerformanceTest]
        public void QuickSortUnsafe()
        {
            const int size = 1 << 12;
            var valuesPtr = stackalloc uint[size];
            for (var i = 0; i < size; ++i)
                valuesPtr[i] = (uint)(Random.value * uint.MaxValue);

            Measure.Method(() =>
                {
                    var valuesCopyPtr = stackalloc uint[size];
                    UnsafeUtility.MemCmp(valuesCopyPtr, valuesPtr, sizeof(uint) * size);

                    CoreUnsafeUtils.QuickSort<uint, uint, CoreUnsafeUtils.DefaultKeyGetter<uint>>(valuesCopyPtr, 0, size - 1);
                })
                .WarmupCount(10)
                .MeasurementCount(10)
                .Run();
        }

        [PerformanceTest]
        public void QuickSortUnsafeUIntArray()
        {
            const int size = 1 << 12;
            var valuesPtr = stackalloc uint[size];
            for (var i = 0; i < size; ++i)
                valuesPtr[i] = (uint)(Random.value * uint.MaxValue);

            var values = new uint[size];

            Measure.Method(() =>
                {
                    fixed (uint* p = &values[0])
                        UnsafeUtility.MemCmp(p, valuesPtr, sizeof(uint) * size);

                    CoreUnsafeUtils.QuickSort(values, 0, values.Length - 1);
                })
                .WarmupCount(10)
                .MeasurementCount(10)
                .Run();
        }
    }
}
