using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Specialized;

namespace BlazorApp1.Pages
{
    partial class Counter
    {
    }

    // Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

    internal static class HelloWasm
    {
        private static int staticInt;
        [ThreadStatic]
        private static int threadStaticInt;

        internal static bool Success;

        public static unsafe int Run()
        {
            Success = true;
            PrintLine("Starting " + 1);

            TestTryCatch();

            TestGvmCallInIf(new GenDerived<string>(), "hello");


            PrintLine("Done");
            return Success ? 100 : -1;
        }

        private static void StartTest(string testDescription)
        {
            PrintString(testDescription + ": ");
        }

        private static void EndTest(bool result, string failMessage = null)
        {
            if (result)
            {
                PassTest();
            }
            else
            {
                FailTest(failMessage);
            }
        }

        internal static void PassTest()
        {
            PrintLine("Ok.");
        }

        internal static void FailTest(string failMessage = null)
        {
            Success = false;
            PrintLine("Failed.");
            if (failMessage != null) PrintLine(failMessage + "-");
        }

        private static unsafe void PrintString(string s)
        {
            int length = s.Length;
            fixed (char* curChar = s)
            {
                for (int i = 0; i < length; i++)
                {
                    TwoByteStr curCharStr = new TwoByteStr();
                    curCharStr.first = (byte)(*(curChar + i));
                    // printf((byte*)&curCharStr, null);
                }
            }
        }

        public static void PrintLine(string s)
        {
            PrintString(s);
            PrintString("\n");
        }

        class GenBase<A>
        {
            public virtual string GMethod1<T>(T t1, T t2) { return "GenBase<" + typeof(A) + ">.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        class GenDerived<A> : GenBase<A>
        {
            public override string GMethod1<T>(T t1, T t2) { return "GenDerived<" + typeof(A) + ">.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }

        private static void TestGvmCallInIf<T>(GenBase<T> g, T p)
        {
            var i = 1;
            if (i == 1)
            {
                g.GMethod1(p, p);
            }
        }

        private static void TestStoreFromGenericMethod()
        {
            StartTest("TestStoreFromGenericMethod");
            var values = new string[1];
            // testing that the generic return value type from the function can be stored in a concrete type
            values = values.AsSpan(0, 1).ToArray();
            EndTest(values.Length == 1);
        }

        private static void TestCallToGenericInterfaceMethod()
        {
            StartTest("Call generic method on interface test");

            TestGenItf implInt = new TestGenItf();
            implInt.Log<object>(new object());
            EndTest(true);
        }

        private static void TestConstrainedValueTypeCallVirt()
        {
            StartTest("Call constrained callvirt");
            //TODO: create simpler test that doesn't need Dictionary<>/KVP<>/Span
            var dict = new Dictionary<KeyValuePair<string, string>, string>();
            var notContainsKey = dict.ContainsKey(new KeyValuePair<string, string>());

            EndTest(!notContainsKey);
        }

        private static void TestBoxToGenericTypeFromDirectMethod()
        {
            StartTest("Callvirt on generic struct boxing to looked up generic type");

            new GetHashCodeCaller<GenStruct<string>, string>().CallValueTypeGetHashCodeFromGeneric(new GenStruct<string>(""));

            PassTest();
        }

        public struct GenStruct<TKey>
        {
            private TKey key;

            public GenStruct(TKey key)
            {
                this.key = key;
            }
        }

        public struct GenStruct2<TKey, T2>
        {
            private TKey key;
            T2 field2;

            public GenStruct2(TKey key, T2 v)
            {
                this.key = key;
                this.field2 = v;
            }
        }

        private static void TestGenericStructHandling()
        {
            StartTest("Casting of generic structs on return and in call params");

            // test return  type is cast
            ActualStructCallParam(new string[0]);

            // test call param is cast
            GenStructCallParam(new GenStructWithImplicitOp<string>());

            // replicate compilation error with https://github.com/dotnet/corert/blob/66fbcd492fbc08db4f472e7e8fa368cb523b38d4/src/System.Private.CoreLib/shared/System/Array.cs#L1482
            GenStructCallParam(CreateGenStructWithImplicitOp<string>(new[] { "" }));

            // replicate compilation error with https://github.com/dotnet/corefx/blob/e99ec129cfd594d53f4390bf97d1d736cff6f860/src/System.Collections.Immutable/src/System/Collections/Immutable/SortedInt32KeyNode.cs#L561
            new GenClassUsingFieldOfInnerStruct<GenClassWithInnerStruct<string>.GenInterfaceOverGenStructStruct>(
                new GenClassWithInnerStruct<string>.GenInterfaceOverGenStructStruct(), null).Create();

            // replicate compilation error with https://github.com/dotnet/runtime/blob/b57a099c1773eeb52d3c663211e275131b4b7938/src/libraries/System.Net.Primitives/src/System/Net/CredentialCache.cs#L328
            new GenClassWithInnerStruct<string>().SetField("");

            PassTest();
        }

        private static GenStructWithImplicitOp<T> CreateGenStructWithImplicitOp<T>(T[] v)
        {
            return new GenStructWithImplicitOp<T>(v);
        }

        private static GenStruct2<T, T2> CreateGenStruct2<T, T2>(T k, T2 v)
        {
            return new GenStruct2<T, T2>(k, v);
        }

        public class GenClassWithInnerStruct<TKey>
        {
            private GenStruct2<TKey, string> structField;

            public void SetField(TKey v)
            {
                structField = HelloWasm.CreateGenStruct2(v, "");
            }

            internal readonly struct GenInterfaceOverGenStructStruct
            {
                // 2 fields to avoid struct collapsing to an i32
                private readonly TKey _firstValue;
                private readonly TKey _otherValue;

                private GenInterfaceOverGenStructStruct(TKey firstElement)
                {
                    _firstValue = firstElement;
                    _otherValue = firstElement;
                }
            }
        }

        public class GenClassUsingFieldOfInnerStruct<T>
        {
            private readonly T _value;
            private GenClassUsingFieldOfInnerStruct<T> _left;

            public GenClassUsingFieldOfInnerStruct(T v, GenClassUsingFieldOfInnerStruct<T> left)
            {
                _value = v;
                _left = left;
            }

            public GenClassUsingFieldOfInnerStruct<T> Create(GenClassUsingFieldOfInnerStruct<T> left = null)
            {
                // some logic to get _value in a temp 
                return new GenClassUsingFieldOfInnerStruct<T>(_value, left ?? _left);
            }
        }

        private static void TestGenericCallInFinally()
        {
            StartTest("calling generic method requiring context from finally block");
            if (GenRequiresContext<string>.Called)
            {
                FailTest("static bool defaulted to true");
            }
            EndTest(CallGenericInFinally<string>());
        }

        private static bool CallGenericInFinally<T>()
        {
            try
            {
                // do nothing
            }
            finally
            {
                GenRequiresContext<T>.Dispose();
            }
            return GenRequiresContext<T>.Called;
        }

        public class GenRequiresContext<T>
        {
            internal static bool Called;

            public static void Dispose()
            {
                Called = true;
            }
        }

        private static void ActualStructCallParam(GenStructWithImplicitOp<string> gs)
        {
        }

        private static void GenStructCallParam<T>(GenStructWithImplicitOp<T> gs)
        {
        }

        public ref struct GenStructWithImplicitOp<TKey>
        {
            private int length;
            private int length2; // just one int field will not create an LLVM struct type, so put another field

            public GenStructWithImplicitOp(TKey[] key)
            {
                length = key.Length;
                length2 = length;
            }

            public static implicit operator GenStructWithImplicitOp<TKey>(TKey[] array) => new GenStructWithImplicitOp<TKey>(array);
        }

        public class GetHashCodeCaller<TKey, TValue>
        {
            public void CallValueTypeGetHashCodeFromGeneric(TKey k)
            {
                k.GetHashCode();
            }
        }

        public interface ITestGenItf
        {
            bool Log<TState>(TState state);
        }

        public class TestGenItf : ITestGenItf
        {
            public bool Log<TState>(TState state)
            {
                return true;
            }
        }

        private static void TestArgsWithMixedTypesAndExceptionRegions()
        {
            new MixedArgFuncClass().MixedArgFunc(1, null, 2, null);
        }

        class MixedArgFuncClass
        {
            public void MixedArgFunc(int firstInt, object shadowStackArg, int secondInt, object secondShadowStackArg)
            {
                HelloWasm.StartTest("MixedParamFuncWithExceptionRegions does not overwrite args");
                bool ok = true;
                int p1 = firstInt;
                try // add a try/catch to get _exceptionRegions.Length > 0 and copy stack args to shadow stack
                {
                    if (shadowStackArg != null)
                    {
                        FailTest("shadowStackArg != null");
                        ok = false;
                    }
                }
                catch (Exception)
                {
                    throw;
                }
                if (p1 != 1)
                {
                    FailTest("p1 not 1, was ");
                    PrintLine(p1.ToString());
                    ok = false;
                }

                if (secondInt != 2)
                {
                    FailTest("secondInt not 2, was ");
                    PrintLine(secondInt.ToString());
                    ok = false;
                }
                if (secondShadowStackArg != null)
                {
                    FailTest("secondShadowStackArg != null");
                    ok = false;
                }
                if (ok)
                {
                    PassTest();
                }
            }
        }

        private static void TestTryCatch()
        {
            // break out the individual tests to their own methods to make looking at the funclets easier
            TestTryCatchNoException();

            TestTryCatchThrowException(new Exception());

            TestTryCatchExceptionFromCall();

            TestCatchExceptionType();

            TestTryFinallyThrowException();

            TestTryCatchWithCallInIf();

            TestThrowInCatch();

            TestExceptionInGvmCall();

            TestCatchHandlerNeedsGenericContext();

            TestFilterHandlerNeedsGenericContext();

            TestFilter();

            TestFilterNested();

            TestCatchAndThrow();

            TestRethrow();
        }

        private static void TestTryCatchNoException()
        {
            bool caught = false;
            StartTest("Catch not called when no exception test");
            try
            {
                new Exception();
            }
            catch (Exception)
            {
                caught = true;
            }
            EndTest(!caught);
        }

        // pass the exception to avoid a call/invoke for that ctor in this function
        private static void TestTryCatchThrowException(Exception e)
        {
            bool caught = false;
            StartTest("Catch called when exception thrown test");
            try
            {
                throw e;
            }
            catch (Exception)
            {
                PrintLine("caught");
                caught = true;
            }
            EndTest(caught);
        }

        static bool finallyCalled;
        private static void TestTryFinallyThrowException()
        {
            finallyCalled = false;
            StartTest("Try/Finally calls finally when exception thrown test");
            try
            {
                TryFinally();
            }
            catch (Exception)
            {

            }
            EndTest(finallyCalled);
        }

        private static void TryFinally()
        {
            try
            {
                throw new Exception();
            }
            finally
            {
                finallyCalled = true;
            }
        }

        private static void TestTryCatchExceptionFromCall()
        {
            bool caught = false;
            StartTest("Catch called when exception thrown from call");
            try
            {
                ThrowException(new Exception());
            }
            catch (Exception)
            {
                caught = true;
            }
            EndTest(caught);
        }

        private static void TestCatchExceptionType()
        {
            int i = 1;
            StartTest("Catch called for exception type and order");
            try
            {
                throw new NullReferenceException("test"); // the parameterless ctor is causing some unexplained memory corruption with the EHInfo pointers...
            }
            catch (ArgumentException)
            {
                i += 10;
            }
            catch (NullReferenceException e)
            {
                if (e.Message == "test")
                {
                    i += 100;
                }
            }
            catch (Exception)
            {
                i += 1000;
            }
            EndTest(i == 101);
        }

        private static void TestTryCatchWithCallInIf()
        {
            int i = 1;
            bool caught = false;
            StartTest("Test invoke when last instruction in if block");
            try
            {
                if (i == 1)
                {
                    PrintString("");
                }
            }
            catch
            {
                caught = true;
            }
            EndTest(!caught);
        }

        private static void TestThrowInCatch()
        {
            int i = 0;
            StartTest("Throw exception in catch");
            Exception outer = new Exception();
            Exception inner = new Exception();
            try
            {
                ThrowException(outer);
            }
            catch
            {
                i += 1;
                try
                {
                    ThrowException(inner);
                }
                catch (Exception e)
                {
                    if (object.ReferenceEquals(e, inner)) i += 10;
                }
            }
            EndTest(i == 11);
        }

        private static void TestExceptionInGvmCall()
        {
            StartTest("TestExceptionInGvmCall");

            //new DerivedThrows<string>().GMethod1("1", (string) null);
            var x = new DerivedThrows<string>();
            var shouldBeFalse = CatchGvmThrownException(new GenBase<string>(), (string)null);
            var shouldBeTrue = CatchGvmThrownException(new DerivedThrows<string>(), (string)null);

            EndTest(shouldBeTrue && !shouldBeFalse);
            //EndTest(true);
        }

        private static unsafe void TestFilter()
        {
            StartTest("TestFilter");

            int counter = 0;
            try
            {
                counter++;
                throw new Exception("Testing filter");
            }
            catch (Exception e) when (e.Message == "Testing filter" && counter++ > 0)
            {
                if (e.Message == "Testing filter")
                {
                    counter++;
                }
                counter++;
            }
            EndTest(counter == 4);
        }

        static string exceptionFlowSequence = "";
        private static void TestFilterNested()
        {
            StartTest("TestFilterNested");
            foreach (var exception in new Exception[]
                {new ArgumentException(), new Exception(), new NullReferenceException()})
            {
                try
                {
                    try
                    {
                        try
                        {
                            throw exception;
                        }
                        catch (NullReferenceException) when (Print("inner"))
                        {
                            exceptionFlowSequence += "In inner catch";
                        }
                    }
                    catch (ArgumentException)
                    {
                        exceptionFlowSequence += "In middle catch";
                    }
                }
                catch (Exception) when (Print("outer"))
                {
                    exceptionFlowSequence += "In outer catch";
                }
            }
            PrintLine(exceptionFlowSequence);
            EndTest(exceptionFlowSequence == @"In middle catchRunning outer filterIn outer catchRunning inner filterIn inner catch");
        }

        private static void TestRethrow()
        {
            StartTest("Test rethrow");
            int caught = 0;
            try
            {
                try
                {
                    throw new Exception("first");
                }
                catch
                {
                    caught++;
                    throw;
                }
            }
            catch (Exception e)
            {
                if (e.Message == "first")
                {
                    caught++;
                }
            }
            EndTest(caught == 2);
        }

        private static void TestCatchAndThrow()
        {
            StartTest("Test catch and throw different exception");
            int caught = 0;
            try
            {
                try
                {
                    throw new Exception("first");
                }
                catch
                {
                    caught += 1;
                    throw new Exception("second");
                }
            }
            catch (Exception e)
            {
                if (e.Message == "second")
                {
                    caught += 10;
                }
            }
            EndTest(caught == 11);
        }

        static bool Print(string s)
        {
            exceptionFlowSequence += $"Running {s} filter";
            return true;
        }

        class DerivedThrows<A> : GenBase<A>
        {
            public override string GMethod1<T>(T t1, T t2) { throw new Exception("ToStringThrows"); }
        }

        private static bool CatchGvmThrownException<T>(GenBase<T> g, T p)
        {
            try
            {
                var i = 1;
                if (i == 1)
                {
                    g.GMethod1(p, p);
                }
            }
            catch (Exception e)
            {
                return e.Message == "ToStringThrows"; // also testing here that we can return a value out of a catch
            }
            return false;
        }

        private static void TestCatchHandlerNeedsGenericContext()
        {
            StartTest("Catch handler can access generic context");
            DerivedCatches<object> c = new DerivedCatches<object>();
            EndTest(c.GvmInCatch<string>("a", "b") == "GenBase<System.Object>.GMethod1<System.String>(a,b)");
        }

        private static void TestFilterHandlerNeedsGenericContext()
        {
            StartTest("Filter funclet can access generic context");
            DerivedCatches<object> c = new DerivedCatches<object>();
            EndTest(c.GvmInFilter<string>("a", "b"));
        }

        class DerivedCatches<A> : GenBase<A>
        {
            public string GvmInCatch<T>(T t1, T t2)
            {
                try
                {
                    throw new Exception();
                }
                catch (Exception)
                {
                    return GMethod1(t1, t2);
                }
            }

            public bool GvmInFilter<T>(T t1, T t2)
            {
                try
                {
                    throw new Exception();
                }
                catch when (GMethod1(t1, t2) == "GenBase<System.Object>.GMethod1<System.String>(a,b)")
                {
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static void ThrowException(Exception e)
        {
            throw e;
        }

        private static unsafe void TestSByteExtend()
        {
            StartTest("SByte extend");
            sbyte s = -1;
            int x = (int)s;
            sbyte s2 = 1;
            int x2 = (int)s2;
            if (x == -1 && x2 == 1)
            {
                PassTest();
            }
            else
            {
                FailTest("Expected -1 and 1 but got " + x.ToString() + " and " + x2.ToString());
            }

            StartTest("SByte left shift");
            x = (int)(s << 1);
            if (x == -2)
            {
                PassTest();
            }
            else
            {
                FailTest("Expected -2 but got " + x.ToString());
            }

            sbyte minus1 = -1;
            StartTest("Negative SByte op");
            if ((s & minus1) == -1)
            {
                PassTest();
            }
            else
            {
                FailTest();
            }

            StartTest("Negative SByte br");
            if (s == -1) // this only creates the bne opcode, which it is testing, in Release mode.
            {
                PassTest();
            }
            else
            {
                FailTest();
            }
        }

        public static void TestSharedDelegate()
        {
            StartTest("Shared Delegate");
            var shouldBeFalse = SampleClassWithGenericDelegate.CallDelegate(new object[0]);
            var shouldBeTrue = SampleClassWithGenericDelegate.CallDelegate(new object[1]);
            EndTest(!shouldBeFalse && shouldBeTrue);
        }

        internal static void TestUlongUintMultiply()
        {
            StartTest("Test ulong/int multiplication");
            uint a = 0x80000000;
            uint b = 2;
            ulong f = ((ulong)a * b);
            EndTest(f == 0x100000000);
        }

        internal static void TestBoxSingle()
        {
            StartTest("Test box single");
            var fi = typeof(ClassWithFloat).GetField("F");
            fi.SetValue(null, 1.1f);
            EndTest(1.1f == ClassWithFloat.F);
        }

        static void TestInitializeArray()
        {
            StartTest("Test InitializeArray");

            bool[,] bools = new bool[2, 2] {
            {  true,                        true},
            {  false,                       true},
        };

            if (!(bools[0, 0] && bools[0, 1]
                && !bools[1, 0] && bools[0, 1]))
            {
                FailTest("bool initialisation failed");
            }

            double[,] doubles = new double[2, 3]
            {
            {1.0, 1.1, 1.2 },
            {2.0, 2.1, 2.2 },
            };

            if (!(doubles[0, 0] == 1.0 && doubles[0, 1] == 1.1 && doubles[0, 2] == 1.2
                && doubles[1, 0] == 2.0 && doubles[1, 1] == 2.1 && doubles[1, 2] == 2.2
                ))
            {
                FailTest("double initialisation failed");
            }

            PassTest();
        }

        static void TestImplicitUShortToUInt()
        {
            StartTest("test extend of shorts with MSB set");
            uint start;
            start = ReadUInt16();
            EndTest(start == 0x0000828f);
        }

        unsafe static void TestReverseDelegateInvoke()
        {
            // tests the try catch LLVM for reverse delegate invokes
            DelegateToCallFromUnmanaged del = (char* charPtr) =>
            {
                return true;
            };
            int i = 1;
            if (i == 0) // dont actually call it as it doesnt exist, just want the reverse delegate created & compiled
            {
                //SomeExternalUmanagedFunction(del);
            }
        }

        static void TestInterlockedExchange()
        {
            StartTest("InterlockedExchange");
            int exInt1 = 1;
            Interlocked.Exchange(ref exInt1, 2);

            long exLong1 = 1;
            Interlocked.Exchange(ref exLong1, 3);
            EndTest(exInt1 == 2 && exLong1 == 3);
        }

        static void TestThrowIfNull()
        {
            StartTest("TestThrowIfNull");
            ClassForNre c = null;
            var success = true;
            try
            {
                var f = c.F; //field access
                PrintLine("NRE Field load access failed");
                success = false;
            }
            catch (NullReferenceException)
            {
            }
            catch (Exception)
            {
                success = false;
            }
            try
            {
                c.F = 1;
                PrintLine("NRE Field store access failed");
                success = false;
            }
            catch (NullReferenceException)
            {
            }
            catch (Exception)
            {
                success = false;
            }
            try
            {
                var f = c.ToString(); //virtual method access
                PrintLine("NRE virtual method access failed");
                success = false;
            }
            catch (NullReferenceException)
            {
            }
            catch (Exception)
            {
                success = false;
            }

            try
            {
                c.NonVirtual(); //method access
                PrintLine("NRE non virtual method access failed");
                success = false;
            }
            catch (NullReferenceException)
            {
            }
            catch (Exception)
            {
                success = false;
            }

            EndTest(success);
        }

#if TARGET_WINDOWS
    private static void TestCkFinite()
    {
        // includes tests from https://github.com/dotnet/coreclr/blob/9b0a9fd623/tests/src/JIT/IL_Conformance/Old/Base/ckfinite.il4
        StartTest("CkFiniteTests");
        if (!CkFiniteTest.CkFinite32(0) || !CkFiniteTest.CkFinite32(1) ||
            !CkFiniteTest.CkFinite32(100) || !CkFiniteTest.CkFinite32(-100) ||
            !CkFinite32(0x7F7FFFC0) || CkFinite32(0xFF800000) ||  // use converter function to get the float equivalent of this bits
            CkFinite32(0x7FC00000) && !CkFinite32(0xFF7FFFFF) ||
            CkFinite32(0x7F800000))
        {
            FailTest("one or more 32 bit tests failed");
            return;
        }

        if (!CkFiniteTest.CkFinite64(0) || !CkFiniteTest.CkFinite64(1) ||
            !CkFiniteTest.CkFinite64(100) || !CkFiniteTest.CkFinite64(-100) ||
            CkFinite64(0x7FF0000000000000) || CkFinite64(0xFFF0000000000000) ||
            CkFinite64(0x7FF8000000000000) || !CkFinite64(0xFFEFFFFFFFFFFFFF))
        {
            FailTest("one or more 64 bit tests failed.");
            return;
        }
        PassTest();
    }

    private static unsafe bool CkFinite32(uint value)
    {
        return CkFiniteTest.CkFinite32 (* (float*)(&value));
    }

    private static unsafe bool CkFinite64(ulong value)
    {
        return CkFiniteTest.CkFinite64(*(double*)(&value));
    }
#endif

        static void TestIntOverflows()
        {
            TestCharInOvf();

            TestSignedIntAddOvf();

            TestSignedLongAddOvf();

            TestUnsignedIntAddOvf();

            TestUnsignedLongAddOvf();

            TestSignedIntSubOvf();

            TestSignedLongSubOvf();

            TestUnsignedIntSubOvf();

            TestUnsignedLongSubOvf();

            TestUnsignedIntMulOvf();

            TestUnsignedLongMulOvf();

            TestSignedIntMulOvf();

            TestSignedLongMulOvf();

            TestSignedToSignedNativeIntConvOvf();

            TestUnsignedToSignedNativeIntConvOvf();

            TestSignedToUnsignedNativeIntConvOvf();

            TestI1ConvOvf();

            TestUnsignedI1ConvOvf();

            TestI2ConvOvf();

            TestUnsignedI2ConvOvf();

            TestI4ConvOvf();

            TestUnsignedI4ConvOvf();

            TestI8ConvOvf();

            TestUnsignedI8ConvOvf();
        }

        private static void TestSignedToSignedNativeIntConvOvf()
        {
            // TODO: when use of nint is available
        }

        private static void TestUnsignedToSignedNativeIntConvOvf()
        {
            // TODO: when use of nuint is available
        }

        private static unsafe void TestSignedToUnsignedNativeIntConvOvf()
        {
            StartTest("Test unsigned native int Conv_Ovf"); // TODO : wasm64
            int thrown = 0;
            long i = 1;
            void* converted;
            checked { converted = (void*)i; }
            if (converted != new IntPtr(1).ToPointer()) FailTest("Test unsigned native int Conv_Ovf conversion failed");
            try
            {
                i = uint.MaxValue + 1L;
                checked { converted = (void*)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                i = -1;
                checked { converted = (void*)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            if (thrown != 2) FailTest("Test unsigned native int Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
            EndTest(true);

        }

        private static void TestI1ConvOvf()
        {
            StartTest("Test I1 Conv_Ovf");
            int thrown = 0;
            int i = 1;
            float f = 127.9F;
            sbyte converted;
            checked { converted = (sbyte)i; }
            if (converted != 1) FailTest("Test I1 Conv_Ovf conversion failed" + converted.ToString());
            checked { converted = (sbyte)(-1); }
            checked { converted = (sbyte)f; }
            checked { converted = (sbyte)((float)-128.5F); }
            try
            {
                i = sbyte.MaxValue + 1;
                checked { converted = (sbyte)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                i = sbyte.MinValue - 1;
                checked { converted = (sbyte)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (float)(sbyte.MaxValue + 1);
                checked { converted = (sbyte)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (float)(sbyte.MinValue - 1);
                checked { converted = (sbyte)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            if (thrown != 4) FailTest("Test I1 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
            EndTest(true);
        }

        private static void TestUnsignedI1ConvOvf()
        {
            StartTest("Test unsigned I1 Conv_Ovf");
            int thrown = 0;
            int i = 1;
            float f = 255.9F;
            byte converted;
            checked { converted = (byte)i; }
            if (converted != 1) FailTest("Test unsigned I1 Conv_Ovf conversion failed" + converted.ToString());
            checked { converted = (byte)f; }
            try
            {
                i = byte.MaxValue + 1;
                checked { converted = (byte)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                i = -1;
                checked { converted = (byte)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (float)(byte.MaxValue + 1);
                checked { converted = (byte)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = -1f;
                checked { converted = (byte)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            if (thrown != 4) FailTest("Test unsigned I1 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
            EndTest(true);
        }

        private static void TestI2ConvOvf()
        {
            StartTest("Test I2 Conv_Ovf");
            int thrown = 0;
            int i = 1;
            float f = 32767.9F;
            Int16 converted;
            checked { converted = (Int16)i; }
            if (converted != 1) FailTest("Test I2 Conv_Ovf conversion failed" + converted.ToString());
            checked { converted = (Int16)(-1); }
            checked { converted = (Int16)f; }
            checked { converted = (Int16)((float)-32768.5F); }
            try
            {
                i = Int16.MaxValue + 1;
                checked { converted = (Int16)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                i = Int16.MinValue - 1;
                checked { converted = (Int16)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (float)(Int16.MaxValue + 1);
                checked { converted = (Int16)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (float)(Int16.MinValue - 1);
                checked { converted = (Int16)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            if (thrown != 4) FailTest("Test I2 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
            EndTest(true);
        }

        private static void TestUnsignedI2ConvOvf()
        {
            StartTest("Test unsigned I2 Conv_Ovf");
            int thrown = 0;
            int i = 1;
            float f = 65535.9F;
            UInt16 converted;
            checked { converted = (UInt16)i; }
            if (converted != 1) FailTest("Test unsigned I2 Conv_Ovf conversion failed" + converted.ToString());
            checked { converted = (UInt16)f; }
            try
            {
                i = UInt16.MaxValue + 1;
                checked { converted = (UInt16)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                i = -1;
                checked { converted = (UInt16)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                short s = -1; // test overflow check is not reliant on different widths
                checked { converted = (UInt16)s; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (float)(UInt16.MaxValue + 1);
                checked { converted = (UInt16)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = -1f;
                checked { converted = (UInt16)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            if (thrown != 5) FailTest("Test unsigned I2 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
            EndTest(true);
        }

        private static void TestI4ConvOvf()
        {
            StartTest("Test I4 Conv_Ovf");
            int thrown = 0;
            long i = 1;
            double f = 2147483647.9d;
            int converted;
            checked { converted = (int)i; }
            if (converted != 1) FailTest("Test I4 Conv_Ovf conversion failed" + converted.ToString());
            checked { converted = (int)(-1); }
            checked { converted = (int)f; }
            checked { converted = (int)((double)-2147483648.9d); }
            try
            {
                i = int.MaxValue + 1L;
                checked { converted = (int)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                i = int.MinValue - 1L;
                checked { converted = (int)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (double)(int.MaxValue + 1L);
                checked { converted = (int)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (double)(int.MinValue - 1L);
                checked { converted = (int)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            if (thrown != 4) FailTest("Test I4 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
            EndTest(true);
        }

        private static void TestUnsignedI4ConvOvf()
        {
            StartTest("Test unsigned I4 Conv_Ovf");
            int thrown = 0;
            long i = 1;
            double f = 4294967294.9d;
            uint converted;
            checked { converted = (uint)i; }
            if (converted != 1) FailTest("Test unsigned I4 Conv_Ovf conversion failed" + converted.ToString());
            checked { converted = (uint)f; }
            try
            {
                i = uint.MaxValue + 1L;
                checked { converted = (uint)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                i = -1;
                checked { converted = (uint)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (double)(uint.MaxValue + 1L);
                checked { converted = (uint)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = -1d;
                checked { converted = (uint)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            if (thrown != 4) FailTest("Test unsigned I4 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
            EndTest(true);
        }

        private static void TestI8ConvOvf()
        {
            StartTest("Test I8 Conv_Ovf");
            int thrown = 0;
            long i = 1;
            double f = 9223372036854774507.9d; /// not a precise check
            long converted;
            checked { converted = (long)i; }
            if (converted != 1) FailTest("Test I8 Conv_Ovf conversion failed" + converted.ToString());
            checked { converted = (long)(-1); }
            checked { converted = (long)f; }
            checked { converted = (long)((double)-9223372036854776508d); } // not a precise check
            try
            {
                ulong ul = long.MaxValue + (ulong)1;
                checked { converted = (int)ul; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (double)(long.MaxValue) + 1000d; // need to get into the next representable double
                checked { converted = (int)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (double)(long.MinValue) - 2000d; // need to get into the next representable double
                checked { converted = (long)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            if (thrown != 3) FailTest("Test I8 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
            EndTest(true);
        }

        private static void TestUnsignedI8ConvOvf()
        {
            StartTest("Test unsigned I8 Conv_Ovf");
            int thrown = 0;
            long i = 1;
            double f = 18446744073709540015.9d; // not a precise check
            ulong converted;
            checked { converted = (ulong)i; }
            if (converted != 1) FailTest("Test unsigned I8 Conv_Ovf conversion failed" + converted.ToString());
            checked { converted = (ulong)f; }
            try
            {
                i = -1;
                checked { converted = (ulong)i; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = (double)(ulong.MaxValue) + 2000d; // need to get into the next representable double
                checked { converted = (ulong)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            try
            {
                f = -1d;
                checked { converted = (ulong)f; }
            }
            catch (OverflowException)
            {
                thrown++;
            }
            if (thrown != 3) FailTest("Test unsigned I8 Conv_Ovf not all cases were thrown  " + thrown.ToString()); ;
            EndTest(true);
        }

        private static void TestSignedLongAddOvf()
        {
            StartTest("Test long add overflows");
            bool thrown;
            long op64l = 1;
            long op64r = long.MaxValue;
            thrown = false;
            try
            {
                long res = checked(op64l + op64r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i64 addition of positive number");
                return;
            }
            thrown = false;
            op64l = long.MinValue; // add negative to overflow below the MinValue
            op64r = -1;
            try
            {
                long res = checked(op64l + op64r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i64 addition of negative number");
                return;
            }
            EndTest(true);
        }

        private static void TestCharInOvf()
        {
            // Just checks the compiler can handle the char type
            // This was failing for https://github.com/dotnet/corert/blob/f542d97f26e87f633310e67497fb01dad29987a5/src/System.Private.CoreLib/shared/System/Environment.Unix.cs#L111
            StartTest("Test char add overflows");
            char opChar = '1';
            int op32r = 2;
            if (checked(opChar + op32r) != 51)
            {
                FailTest("No overflow for char failed"); // check not always throwing an exception
                return;
            }
            PassTest();
        }

        private static void TestSignedIntAddOvf()
        {
            StartTest("Test int add overflows");
            bool thrown;
            int op32l = 1;
            int op32r = 2;
            if (checked(op32l + op32r) != 3)
            {
                FailTest("No overflow failed"); // check not always throwing an exception
                return;
            }
            op32l = 1;
            op32r = int.MaxValue;
            thrown = false;
            try
            {
                int res = checked(op32l + op32r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i32 addition of positive number");
                return;
            }

            thrown = false;
            op32l = int.MinValue; // add negative to overflow below the MinValue
            op32r = -1;
            try
            {
                int res = checked(op32l + op32r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i32 addition of negative number");
                return;
            }
            PassTest();
        }

        private static void TestUnsignedIntAddOvf()
        {
            StartTest("Test uint add overflows");
            bool thrown;
            uint op32l = 1;
            uint op32r = 2;
            if (checked(op32l + op32r) != 3)
            {
                FailTest("No overflow failed"); // check not always throwing an exception
                return;
            }
            op32l = 1;
            op32r = uint.MaxValue;
            thrown = false;
            try
            {
                uint res = checked(op32l + op32r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for unsigned i32 addition of positive number");
                return;
            }
            PassTest();
        }

        private static void TestUnsignedLongAddOvf()
        {
            StartTest("Test ulong add overflows");
            bool thrown;
            ulong op64l = 1;
            ulong op64r = 2;
            if (checked(op64l + op64r) != 3)
            {
                FailTest("No overflow failed"); // check not always throwing an exception
                return;
            }
            op64l = 1;
            op64r = ulong.MaxValue;
            thrown = false;
            try
            {
                ulong res = checked(op64l + op64r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for unsigned i64 addition of positive number");
                return;
            }
            PassTest();
        }

        private static void TestSignedLongSubOvf()
        {
            StartTest("Test long sub overflows");
            bool thrown;
            long op64l = -2;
            long op64r = long.MaxValue;
            thrown = false;
            try
            {
                long res = checked(op64l - op64r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i64 substraction of positive number");
                return;
            }
            thrown = false;
            op64l = long.MaxValue; // subtract negative to overflow above the MaxValue
            op64r = -1;
            try
            {
                long res = checked(op64l - op64r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i64 addition of negative number");
                return;
            }
            EndTest(true);
        }

        private static void TestSignedIntSubOvf()
        {
            StartTest("Test int sub overflows");
            bool thrown;
            int op32l = 5;
            int op32r = 2;
            if (checked(op32l - op32r) != 3)
            {
                FailTest("No overflow failed"); // check not always throwing an exception
                return;
            }
            op32l = -2;
            op32r = int.MaxValue;
            thrown = false;
            try
            {
                int res = checked(op32l - op32r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i32 subtraction of positive number");
                return;
            }

            thrown = false;
            op32l = int.MaxValue; // subtract negative to overflow above the MaxValue
            op32r = -1;
            try
            {
                int res = checked(op32l - op32r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i32 subtraction of negative number");
                return;
            }
            PassTest();
        }

        private static void TestUnsignedIntSubOvf()
        {
            StartTest("Test uint sub overflows");
            bool thrown;
            uint op32l = 5;
            uint op32r = 2;
            if (checked(op32l - op32r) != 3)
            {
                FailTest("No overflow failed"); // check not always throwing an exception
                return;
            }
            op32l = 0;
            op32r = 1;
            thrown = false;
            try
            {
                uint res = checked(op32l - op32r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for unsigned i32 subtraction of positive number");
                return;
            }
            PassTest();
        }

        private static void TestUnsignedLongSubOvf()
        {
            StartTest("Test ulong sub overflows");
            bool thrown;
            ulong op64l = 5;
            ulong op64r = 2;
            if (checked(op64l - op64r) != 3)
            {
                FailTest("No overflow failed"); // check not always throwing an exception
                return;
            }
            op64l = 0;
            op64r = 1;
            thrown = false;
            try
            {
                ulong res = checked(op64l - op64r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for unsigned i64 addition of positive number");
                return;
            }
            PassTest();
        }

        private static void TestUnsignedIntMulOvf()
        {
            StartTest("Test uint multiply overflows");
            bool thrown;
            uint op32l = 10;
            uint op32r = 20;
            if (checked(op32l * op32r) != 200)
            {
                FailTest("No overflow failed"); // check not always throwing an exception
                return;
            }
            op32l = 2;
            op32r = (uint.MaxValue >> 1) + 1;
            thrown = false;
            try
            {
                uint res = checked(op32l * op32r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for unsigned i32 multiply of numbers");
                return;
            }
            op32l = 0;
            op32r = 0; // check does a division so make sure this case is handled
            thrown = false;
            try
            {
                uint res = checked(op32l * op32r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (thrown)
            {
                FailTest("exception not thrown for unsigned i32 multiply of zeros");
                return;
            }
            PassTest();
        }

        private static void TestUnsignedLongMulOvf()
        {
            StartTest("Test ulong multiply overflows");
            bool thrown;
            ulong op64l = 10;
            ulong op64r = 20;
            if (checked(op64l * op64r) != 200L)
            {
                FailTest("No overflow failed"); // check not always throwing an exception
                return;
            }
            op64l = 2;
            op64r = (ulong.MaxValue >> 1) + 1;
            thrown = false;
            try
            {
                ulong res = checked(op64l * op64r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for unsigned i64 multiply of numbers");
                return;
            }
            op64l = 0;
            op64r = 0; // check does a division so make sure this case is handled
            thrown = false;
            try
            {
                ulong res = checked(op64l * op64r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (thrown)
            {
                FailTest("exception not thrown for unsigned i64 multiply of zeros");
                return;
            }
            PassTest();
        }

        private static void TestSignedIntMulOvf()
        {
            StartTest("Test int multiply overflows");
            bool thrown;
            int op32l = 10;
            int op32r = -20;
            if (checked(op32l * op32r) != -200)
            {
                FailTest("No overflow failed"); // check not always throwing an exception
                return;
            }
            op32l = 2;
            op32r = (int.MaxValue >> 1) + 1;
            thrown = false;
            try
            {
                int res = checked(op32l * op32r);
                PrintLine("should have overflow but was " + res.ToString());
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i32 multiply overflow");
                return;
            }
            op32l = 2;
            op32r = (int.MinValue >> 1) - 1;
            thrown = false;
            try
            {
                int res = checked(op32l * op32r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i32 multiply underflow");
                return;
            }
            op32l = 0;
            op32r = 0; // check does a division so make sure this case is handled
            thrown = false;
            try
            {
                int res = checked(op32l * op32r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (thrown)
            {
                FailTest("exception not thrown for signed i32 multiply of zeros");
                return;
            }

            PassTest();
        }

        private static void TestSignedLongMulOvf()
        {
            StartTest("Test long multiply overflows");
            bool thrown;
            long op64l = 10;
            long op64r = -20;
            if (checked(op64l * op64r) != -200)
            {
                FailTest("No overflow failed"); // check not always throwing an exception
                return;
            }
            op64l = 2;
            op64r = (long.MaxValue >> 1) + 1;
            thrown = false;
            try
            {
                long res = checked(op64l * op64r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i64 multiply overflow");
                return;
            }
            op64l = 2;
            op64r = (long.MinValue >> 1) - 1;
            thrown = false;
            try
            {
                long res = checked(op64l * op64r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (!thrown)
            {
                FailTest("exception not thrown for signed i64 multiply underflow");
                return;
            }
            op64l = 0;
            op64r = 0; // check does a division so make sure this case is handled
            thrown = false;
            try
            {
                long res = checked(op64l * op64r);
            }
            catch (OverflowException)
            {
                thrown = true;
            }
            if (thrown)
            {
                FailTest("exception not thrown for signed i64 multiply of zeros");
                return;
            }
            PassTest();
        }

        private static unsafe void TestStackTrace()
        {
            StartTest("Test StackTrace");
#if DEBUG
        EndTest(new StackTrace().ToString().Contains("TestStackTrace"));
#else
            EndTest(new StackTrace().ToString().Contains("wasm-function"));
#endif
        }

        static void TestDefaultConstructorOf()
        {
            StartTest("Test DefaultConstructorOf");
            var c = Activator.CreateInstance<ClassForNre>();
            EndTest(c != null);
        }

        internal struct LargeArrayBuilder<T>
        {
            private readonly int _maxCapacity;

            public LargeArrayBuilder(bool initialize)
                : this(maxCapacity: int.MaxValue)
            {
            }

            public LargeArrayBuilder(int maxCapacity)
                : this()
            {
                _maxCapacity = maxCapacity;
            }
        }

        static void TestStructUnboxOverload()
        {
            StartTest("Test DefaultConstructorOf");
            var s = new LargeArrayBuilder<string>(true);
            var s2 = new LargeArrayBuilder<string>(1);
            EndTest(true); // testing compilation 
        }

        static void TestGetSystemArrayEEType()
        {
            StartTest("Test can call GetSystemArrayEEType through CalliIntrinsic");
            IList e = new string[] { "1" };
            foreach (string s in e)
            {
            }
            EndTest(true); // testing compilation 
        }

        static void TestBoolCompare()
        {
            StartTest("Test Bool.Equals");
            bool expected = true;
            bool actual = true;
            EndTest(expected.Equals(actual));
        }

        static ushort ReadUInt16()
        {
            // something with MSB set
            return 0x828f;
        }

        unsafe internal delegate bool DelegateToCallFromUnmanaged(char* charPtr);

    }

    namespace JSInterop
    {
        internal static class InternalCalls
        {
            [DllImport("*", EntryPoint = "corert_wasm_invoke_js_unmarshalled")]
            private static extern IntPtr InvokeJSUnmarshalledInternal(string js, int length, IntPtr p1, IntPtr p2, IntPtr p3, out string exception);

            public static IntPtr InvokeJSUnmarshalled(out string exception, string js, IntPtr p1, IntPtr p2, IntPtr p3)
            {
                return InvokeJSUnmarshalledInternal(js, js.Length, p1, p2, p3, out exception);
            }
        }
    }

    public class ClassForNre
    {
        public int F;
        public void NonVirtual() { }
    }


    public class ClassWithFloat
    {
        public static float F;
    }

    public class SampleClassWithGenericDelegate
    {
        public static bool CallDelegate<T>(T[] items)
        {
            return new Stack<T>(items).CallDelegate(DoWork);
        }

        public static bool DoWork<T>(T[] items)
        {
            HelloWasm.PrintLine("DoWork");
            return items.Length > 0;
        }
    }

    public class Stack<T>
    {
        T[] items;

        public Stack(T[] items)
        {
            this.items = items;
        }

        public bool CallDelegate(StackDelegate d)
        {
            HelloWasm.PrintLine("CallDelegate");
            HelloWasm.PrintLine(items.Length.ToString());
            return d(items);
        }

        public delegate bool StackDelegate(T[] items);
    }

    public struct TwoByteStr
    {
        public byte first;
        public byte second;
    }

    public struct BoxStubTest
    {
        public string Value;
        public override string ToString()
        {
            return Value;
        }

        public string GetValue()
        {
            HelloWasm.PrintLine("BoxStubTest.GetValue called");
            HelloWasm.PrintLine(Value);
            return Value;
        }
    }

    public class TestClass
    {
        public string TestString { get; set; }
        public int TestInt { get; set; }

        public TestClass(int number)
        {
            if (number != 1337)
                throw new Exception();
        }

        public void TestMethod(string str)
        {
            TestString = str;
            if (TestString == str)
                HelloWasm.PrintLine("Instance method call test: Ok.");
        }
        public virtual void TestVirtualMethod(string str)
        {
            HelloWasm.PrintLine("Virtual Slot Test: Ok If second");
        }

        public virtual void TestVirtualMethod2(string str)
        {
            HelloWasm.PrintLine("Virtual Slot Test 2: Ok");
        }

        public int InstanceDelegateTarget()
        {
            return TestInt;
        }

        public virtual void VirtualDelegateTarget()
        {
            HelloWasm.FailTest("Virtual delegate incorrectly dispatched to base.");
        }
    }

}
