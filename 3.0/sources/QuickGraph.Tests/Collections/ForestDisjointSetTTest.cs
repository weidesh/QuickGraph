// <copyright file="ForestDisjointSetTTest.cs" company="Jonathan de Halleux">Copyright http://www.codeplex.com/quickgraph</copyright>
using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QuickGraph.Collections;

namespace QuickGraph.Collections
{
    /// <summary>This class contains parameterized unit tests for ForestDisjointSet`1</summary>
    [TestClass]
    [PexClass(typeof(ForestDisjointSet<>))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    public partial class ForestDisjointSetTTest
    {
        [PexMethod(MaxConstraintSolverTime = 2, MaxConditions = 1000, MaxRunsWithoutNewTests = 200, MaxBranches = 20000)]
        public void Unions(int elementCount, [PexAssumeNotNull]int[] unions)
        {
            PexAssume.IsTrue(elementCount > 0);
            PexSymbolicValue.Minimize(elementCount);

            var target = new ForestDisjointSet<int>();
            // fill up with 0..elementCount - 1
            for (int i = 0; i < elementCount; i++)
            {
                target.MakeSet(i);
                Assert.AreEqual(i + 1, target.ElementCount);
                Assert.AreEqual(i + 1, target.SetCount);
            }

            // apply Union for pairs unions[i], unions[i+1]
            for (int i = 0; i + 1 < unions.Length; i+=2)
            {
                var left = unions[i];
                var right= unions[i+1];
                // ignore case where left or right are not in the data struture
                PexAssume.IsTrue(target.Contains(left));
                PexAssume.IsTrue(target.Contains(right));

                var setCount = target.SetCount;
                bool unioned = target.Union(left, right);
                // should be in the same set now
                Assert.IsTrue(target.AreInSameSet(left, right));
                // if unioned, the count decreased by 1
                PexAssert.ImpliesIsTrue(unioned, () => setCount - 1 == target.SetCount);
            }
        }

        /// <summary>Test stub for Contains(!0)</summary>
        [PexMethod]
        [PexGenericArguments(typeof(int))]
        public void Contains<T>(T value)
        {
            var target = new ForestDisjointSet<T>();
            target.MakeSet(value);
            Assert.IsTrue(target.Contains(value));
        }
    }
}
