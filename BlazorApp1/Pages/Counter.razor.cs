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

        private static void TestTryCatch()
        {
            // break out the individual tests to their own methods to make looking at the funclets easier

            TestExceptionInGvmCall();

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


        static bool Print(string s)
        {
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


    }

}
