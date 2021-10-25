#if !NET5_0_OR_GREATER
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*

The xxHash32 implementation is based on the code published by Yann Collet:
https://raw.githubusercontent.com/Cyan4973/xxHash/5c174cfa4e45a42f94082dc0d4539b39696afea1/xxhash.c

  xxHash - Fast Hash algorithm
  Copyright (C) 2012-2016, Yann Collet

  BSD 2-Clause License (http://www.opensource.org/licenses/bsd-license.php)

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions are
  met:

  * Redistributions of source code must retain the above copyright
  notice, this list of conditions and the following disclaimer.
  * Redistributions in binary form must reproduce the above
  copyright notice, this list of conditions and the following disclaimer
  in the documentation and/or other materials provided with the
  distribution.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
  A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
  OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
  SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
  LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
  DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
  THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
  (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
  OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

  You can contact the author at :
  - xxHash homepage: http://www.xxhash.com
  - xxHash source repository : https://github.com/Cyan4973/xxHash

*/

using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace System
{
    // NOTE: This class is a copy from src\Common\src\CoreLib\System\Numerics\BitOperations.cs only for HashCode purposes.
    // Any changes to the BitOperations class should be done in there instead.
    internal static class BitOperations
    {
        /// <summary>
        /// Rotates the specified value left by the specified number of bits.
        /// Similar in behavior to the x86 instruction ROL.
        /// </summary>
        /// <param name="value">The value to rotate.</param>
        /// <param name="offset">The number of bits to rotate by.
        /// Any value outside the range [0..31] is treated as congruent mod 32.</param>
        /// <returns>The rotated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateLeft(uint value, int offset)
            => (value << offset) | (value >> (32 - offset));

        /// <summary>
        /// Rotates the specified value left by the specified number of bits.
        /// Similar in behavior to the x86 instruction ROL.
        /// </summary>
        /// <param name="value">The value to rotate.</param>
        /// <param name="offset">The number of bits to rotate by.
        /// Any value outside the range [0..63] is treated as congruent mod 64.</param>
        /// <returns>The rotated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RotateLeft(ulong value, int offset)
            => (value << offset) | (value >> (64 - offset));
    }
    
    internal partial class Interop
    {
        internal static unsafe void GetRandomBytes(byte* buffer, int length)
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            buffer = (byte*)BitConverter.ToUInt32(bytes, 0);
        }
    }

    // xxHash32 is used for the hash code.
    // https://github.com/Cyan4973/xxHash

    public struct HashCode
    {
        private static readonly uint s_seed = GenerateGlobalSeed();

        private const uint Prime1 = 2654435761U;
        private const uint Prime2 = 2246822519U;
        private const uint Prime3 = 3266489917U;
        private const uint Prime4 = 668265263U;
        private const uint Prime5 = 374761393U;

        private uint _v1, _v2, _v3, _v4;
        private uint _queue1, _queue2, _queue3;
        private uint _length;

        private static unsafe uint GenerateGlobalSeed()
        {
            uint result;
            Interop.GetRandomBytes((byte*)&result, sizeof(uint));
            return result;
        }

        public static int Combine<T1>(T1 value1)
        {
            // Provide a way of diffusing bits from something with a limited
            // input hash space. For example, many enums only have a few
            // possible hashes, only using the bottom few bits of the code. Some
            // collections are built on the assumption that hashes are spread
            // over a larger space, so diffusing the bits may help the
            // collection work more efficiently.

            uint hc1 = (uint)(value1?.GetHashCode() ?? 0);

            uint hash = MixEmptyState();
            hash += 4;

            hash = QueueRound(hash, hc1);

            hash = MixFinal(hash);
            return (int)hash;
        }

        public static int Combine<T1, T2>(T1 value1, T2 value2)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
            uint hc2 = (uint)(value2?.GetHashCode() ?? 0);

            uint hash = MixEmptyState();
            hash += 8;

            hash = QueueRound(hash, hc1);
            hash = QueueRound(hash, hc2);

            hash = MixFinal(hash);
            return (int)hash;
        }

        public static int Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
            uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
            uint hc3 = (uint)(value3?.GetHashCode() ?? 0);

            uint hash = MixEmptyState();
            hash += 12;

            hash = QueueRound(hash, hc1);
            hash = QueueRound(hash, hc2);
            hash = QueueRound(hash, hc3);

            hash = MixFinal(hash);
            return (int)hash;
        }

        public static int Combine<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
            uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
            uint hc3 = (uint)(value3?.GetHashCode() ?? 0);
            uint hc4 = (uint)(value4?.GetHashCode() ?? 0);

            Initialize(out uint v1, out uint v2, out uint v3, out uint v4);

            v1 = Round(v1, hc1);
            v2 = Round(v2, hc2);
            v3 = Round(v3, hc3);
            v4 = Round(v4, hc4);

            uint hash = MixState(v1, v2, v3, v4);
            hash += 16;

            hash = MixFinal(hash);
            return (int)hash;
        }

        public static int Combine<T1, T2, T3, T4, T5>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
            uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
            uint hc3 = (uint)(value3?.GetHashCode() ?? 0);
            uint hc4 = (uint)(value4?.GetHashCode() ?? 0);
            uint hc5 = (uint)(value5?.GetHashCode() ?? 0);

            Initialize(out uint v1, out uint v2, out uint v3, out uint v4);

            v1 = Round(v1, hc1);
            v2 = Round(v2, hc2);
            v3 = Round(v3, hc3);
            v4 = Round(v4, hc4);

            uint hash = MixState(v1, v2, v3, v4);
            hash += 20;

            hash = QueueRound(hash, hc5);

            hash = MixFinal(hash);
            return (int)hash;
        }

        public static int Combine<T1, T2, T3, T4, T5, T6>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
            uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
            uint hc3 = (uint)(value3?.GetHashCode() ?? 0);
            uint hc4 = (uint)(value4?.GetHashCode() ?? 0);
            uint hc5 = (uint)(value5?.GetHashCode() ?? 0);
            uint hc6 = (uint)(value6?.GetHashCode() ?? 0);

            Initialize(out uint v1, out uint v2, out uint v3, out uint v4);

            v1 = Round(v1, hc1);
            v2 = Round(v2, hc2);
            v3 = Round(v3, hc3);
            v4 = Round(v4, hc4);

            uint hash = MixState(v1, v2, v3, v4);
            hash += 24;

            hash = QueueRound(hash, hc5);
            hash = QueueRound(hash, hc6);

            hash = MixFinal(hash);
            return (int)hash;
        }

        public static int Combine<T1, T2, T3, T4, T5, T6, T7>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
            uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
            uint hc3 = (uint)(value3?.GetHashCode() ?? 0);
            uint hc4 = (uint)(value4?.GetHashCode() ?? 0);
            uint hc5 = (uint)(value5?.GetHashCode() ?? 0);
            uint hc6 = (uint)(value6?.GetHashCode() ?? 0);
            uint hc7 = (uint)(value7?.GetHashCode() ?? 0);

            Initialize(out uint v1, out uint v2, out uint v3, out uint v4);

            v1 = Round(v1, hc1);
            v2 = Round(v2, hc2);
            v3 = Round(v3, hc3);
            v4 = Round(v4, hc4);

            uint hash = MixState(v1, v2, v3, v4);
            hash += 28;

            hash = QueueRound(hash, hc5);
            hash = QueueRound(hash, hc6);
            hash = QueueRound(hash, hc7);

            hash = MixFinal(hash);
            return (int)hash;
        }

        public static int Combine<T1, T2, T3, T4, T5, T6, T7, T8>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8)
        {
            uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
            uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
            uint hc3 = (uint)(value3?.GetHashCode() ?? 0);
            uint hc4 = (uint)(value4?.GetHashCode() ?? 0);
            uint hc5 = (uint)(value5?.GetHashCode() ?? 0);
            uint hc6 = (uint)(value6?.GetHashCode() ?? 0);
            uint hc7 = (uint)(value7?.GetHashCode() ?? 0);
            uint hc8 = (uint)(value8?.GetHashCode() ?? 0);

            Initialize(out uint v1, out uint v2, out uint v3, out uint v4);

            v1 = Round(v1, hc1);
            v2 = Round(v2, hc2);
            v3 = Round(v3, hc3);
            v4 = Round(v4, hc4);

            v1 = Round(v1, hc5);
            v2 = Round(v2, hc6);
            v3 = Round(v3, hc7);
            v4 = Round(v4, hc8);

            uint hash = MixState(v1, v2, v3, v4);
            hash += 32;

            hash = MixFinal(hash);
            return (int)hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Initialize(out uint v1, out uint v2, out uint v3, out uint v4)
        {
            v1 = s_seed + Prime1 + Prime2;
            v2 = s_seed + Prime2;
            v3 = s_seed;
            v4 = s_seed - Prime1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Round(uint hash, uint input)
        {
            return BitOperations.RotateLeft(hash + input * Prime2, 13) * Prime1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QueueRound(uint hash, uint queuedValue)
        {
            return BitOperations.RotateLeft(hash + queuedValue * Prime3, 17) * Prime4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixState(uint v1, uint v2, uint v3, uint v4)
        {
            return BitOperations.RotateLeft(v1, 1) + BitOperations.RotateLeft(v2, 7) + BitOperations.RotateLeft(v3, 12) + BitOperations.RotateLeft(v4, 18);
        }

        private static uint MixEmptyState()
        {
            return s_seed + Prime5;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixFinal(uint hash)
        {
            hash ^= hash >> 15;
            hash *= Prime2;
            hash ^= hash >> 13;
            hash *= Prime3;
            hash ^= hash >> 16;
            return hash;
        }

        public void Add<T>(T value)
        {
            Add(value?.GetHashCode() ?? 0);
        }

        public void Add<T>(T value, IEqualityComparer<T>? comparer)
        {
            Add(value is null ? 0 : (comparer?.GetHashCode(value) ?? value.GetHashCode()));
        }

        private void Add(int value)
        {
            // The original xxHash works as follows:
            // 0. Initialize immediately. We can't do this in a struct (no
            //    default ctor).
            // 1. Accumulate blocks of length 16 (4 uints) into 4 accumulators.
            // 2. Accumulate remaining blocks of length 4 (1 uint) into the
            //    hash.
            // 3. Accumulate remaining blocks of length 1 into the hash.

            // There is no need for #3 as this type only accepts ints. _queue1,
            // _queue2 and _queue3 are basically a buffer so that when
            // ToHashCode is called we can execute #2 correctly.

            // We need to initialize the xxHash32 state (_v1 to _v4) lazily (see
            // #0) nd the last place that can be done if you look at the
            // original code is just before the first block of 16 bytes is mixed
            // in. The xxHash32 state is never used for streams containing fewer
            // than 16 bytes.

            // To see what's really going on here, have a look at the Combine
            // methods.

            uint val = (uint)value;

            // Storing the value of _length locally shaves of quite a few bytes
            // in the resulting machine code.
            uint previousLength = _length++;
            uint position = previousLength % 4;

            // Switch can't be inlined.

            if (position == 0)
                _queue1 = val;
            else if (position == 1)
                _queue2 = val;
            else if (position == 2)
                _queue3 = val;
            else // position == 3
            {
                if (previousLength == 3)
                    Initialize(out _v1, out _v2, out _v3, out _v4);

                _v1 = Round(_v1, _queue1);
                _v2 = Round(_v2, _queue2);
                _v3 = Round(_v3, _queue3);
                _v4 = Round(_v4, val);
            }
        }

        public int ToHashCode()
        {
            // Storing the value of _length locally shaves of quite a few bytes
            // in the resulting machine code.
            uint length = _length;

            // position refers to the *next* queue position in this method, so
            // position == 1 means that _queue1 is populated; _queue2 would have
            // been populated on the next call to Add.
            uint position = length % 4;

            // If the length is less than 4, _v1 to _v4 don't contain anything
            // yet. xxHash32 treats this differently.

            uint hash = length < 4 ? MixEmptyState() : MixState(_v1, _v2, _v3, _v4);

            // _length is incremented once per Add(Int32) and is therefore 4
            // times too small (xxHash length is in bytes, not ints).

            hash += length * 4;

            // Mix what remains in the queue

            // Switch can't be inlined right now, so use as few branches as
            // possible by manually excluding impossible scenarios (position > 1
            // is always false if position is not > 0).
            if (position > 0)
            {
                hash = QueueRound(hash, _queue1);
                if (position > 1)
                {
                    hash = QueueRound(hash, _queue2);
                    if (position > 2)
                        hash = QueueRound(hash, _queue3);
                }
            }

            hash = MixFinal(hash);
            return (int)hash;
        }

#pragma warning disable 0809
        // Obsolete member 'memberA' overrides non-obsolete member 'memberB'.
        // Disallowing GetHashCode and Equals is by design

        // * We decided to not override GetHashCode() to produce the hash code
        //   as this would be weird, both naming-wise as well as from a
        //   behavioral standpoint (GetHashCode() should return the object's
        //   hash code, not the one being computed).

        // * Even though ToHashCode() can be called safely multiple times on
        //   this implementation, it is not part of the contract. If the
        //   implementation has to change in the future we don't want to worry
        //   about people who might have incorrectly used this type.

        [Obsolete("HashCode is a mutable struct and should not be compared with other HashCodes. Use ToHashCode to retrieve the computed hash code.", error: true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() => throw new NotSupportedException(SR.HashCode_HashCodeNotSupported);

        [Obsolete("HashCode is a mutable struct and should not be compared with other HashCodes.", error: true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object? obj) => throw new NotSupportedException(SR.HashCode_EqualityNotSupported);
#pragma warning restore 0809
    }
}

namespace System.Private.CoreLib
{
    internal static class Strings { }
}

namespace System
{
    using Reflection;
    internal static class SR
    {
        //TODO Add ResourceManager
        private static Resources.ResourceManager s_resourceManager;
        internal static Resources.ResourceManager ResourceManager 
            => s_resourceManager ??= new Resources.ResourceManager(typeof(Private.CoreLib.Strings));

        private static string GetResourceString(string key) 
            => s_resourceManager.GetString(key, CultureInfo.CurrentCulture);

        /// <summary>Name:</summary>
        internal static string @AppDomain_Name => GetResourceString("AppDomain_Name");
        /// <summary>There are no context policies.</summary>
        internal static string @AppDomain_NoContextPolicies => GetResourceString("AppDomain_NoContextPolicies");
        /// <summary>Default principal object cannot be set twice.</summary>
        internal static string @AppDomain_Policy_PrincipalTwice => GetResourceString("AppDomain_Policy_PrincipalTwice");
        /// <summary>Ambiguous implementation found.</summary>
        internal static string @AmbiguousImplementationException_NullMessage => GetResourceString("AmbiguousImplementationException_NullMessage");
        /// <summary>Cannot access member.</summary>
        internal static string @Arg_AccessException => GetResourceString("Arg_AccessException");
        /// <summary>Attempted to read or write protected memory. This is often an indication that other memory is corrupt.</summary>
        internal static string @Arg_AccessViolationException => GetResourceString("Arg_AccessViolationException");
        /// <summary>Error in the application.</summary>
        internal static string @Arg_ApplicationException => GetResourceString("Arg_ApplicationException");
        /// <summary>Value does not fall within the expected range.</summary>
        internal static string @Arg_ArgumentException => GetResourceString("Arg_ArgumentException");
        /// <summary>Specified argument was out of the range of valid values.</summary>
        internal static string @Arg_ArgumentOutOfRangeException => GetResourceString("Arg_ArgumentOutOfRangeException");
        /// <summary>Overflow or underflow in the arithmetic operation.</summary>
        internal static string @Arg_ArithmeticException => GetResourceString("Arg_ArithmeticException");
        /// <summary>Destination array is not long enough to copy all the items in the collection. Check array index and length.</summary>
        internal static string @Arg_ArrayPlusOffTooSmall => GetResourceString("Arg_ArrayPlusOffTooSmall");
        /// <summary>Attempted to access an element as a type incompatible with the array.</summary>
        internal static string @Arg_ArrayTypeMismatchException => GetResourceString("Arg_ArrayTypeMismatchException");
        /// <summary>Array must not be of length zero.</summary>
        internal static string @Arg_ArrayZeroError => GetResourceString("Arg_ArrayZeroError");
        /// <summary>Format of the executable (.exe) or library (.dll) is invalid.</summary>
        internal static string @Arg_BadImageFormatException => GetResourceString("Arg_BadImageFormatException");
        /// <summary>Unable to sort because the IComparer.Compare() method returns inconsistent results. Either a value does not compare equal to itself, or one value repeatedly compared to another value yields different results. IComparer: '{0}'.</summary>
        internal static string @Arg_BogusIComparer => GetResourceString("Arg_BogusIComparer");
        /// <summary>TimeSpan does not accept floating point Not-a-Number values.</summary>
        internal static string @Arg_CannotBeNaN => GetResourceString("Arg_CannotBeNaN");
        /// <summary>String cannot contain a minus sign if the base is not 10.</summary>
        internal static string @Arg_CannotHaveNegativeValue => GetResourceString("Arg_CannotHaveNegativeValue");
        /// <summary>The usage of IKeyComparer and IHashCodeProvider/IComparer interfaces cannot be mixed; use one or the other.</summary>
        internal static string @Arg_CannotMixComparisonInfrastructure => GetResourceString("Arg_CannotMixComparisonInfrastructure");
        /// <summary>Attempt to unload the AppDomain failed.</summary>
        internal static string @Arg_CannotUnloadAppDomainException => GetResourceString("Arg_CannotUnloadAppDomainException");
        /// <summary>Arrays must contain only blittable data in order to be copied to unmanaged memory.</summary>
        internal static string @Arg_CopyNonBlittableArray => GetResourceString("Arg_CopyNonBlittableArray");
        /// <summary>Requested range extends past the end of the array.</summary>
        internal static string @Arg_CopyOutOfRange => GetResourceString("Arg_CopyOutOfRange");
        /// <summary>Error HRESULT E_FAIL has been returned from a call to a COM component.</summary>
        internal static string @Arg_COMException => GetResourceString("Arg_COMException");
        /// <summary>Error occurred during a cryptographic operation.</summary>
        internal static string @Arg_CryptographyException => GetResourceString("Arg_CryptographyException");
        /// <summary>A datatype misalignment was detected in a load or store instruction.</summary>
        internal static string @Arg_DataMisalignedException => GetResourceString("Arg_DataMisalignedException");
        /// <summary>Combination of arguments to the DateTime constructor is out of the legal range.</summary>
        internal static string @Arg_DateTimeRange => GetResourceString("Arg_DateTimeRange");
        /// <summary>Attempted to access a path that is not on the disk.</summary>
        internal static string @Arg_DirectoryNotFoundException => GetResourceString("Arg_DirectoryNotFoundException");
        /// <summary>Decimal byte array constructor requires an array of length four containing valid decimal bytes.</summary>
        internal static string @Arg_DecBitCtor => GetResourceString("Arg_DecBitCtor");
        /// <summary>Attempted to divide by zero.</summary>
        internal static string @Arg_DivideByZero => GetResourceString("Arg_DivideByZero");
        /// <summary>Delegate to an instance method cannot have null 'this'.</summary>
        internal static string @Arg_DlgtNullInst => GetResourceString("Arg_DlgtNullInst");
        /// <summary>Delegates must be of the same type.</summary>
        internal static string @Arg_DlgtTypeMis => GetResourceString("Arg_DlgtTypeMis");
        /// <summary>Duplicate objects in argument.</summary>
        internal static string @Arg_DuplicateWaitObjectException => GetResourceString("Arg_DuplicateWaitObjectException");
        /// <summary>This ExceptionHandlingClause is not a filter.</summary>
        internal static string @Arg_EHClauseNotFilter => GetResourceString("Arg_EHClauseNotFilter");
        /// <summary>Object must be the same type as the enum. The type passed in was '{0}'; the enum type was '{1}'.</summary>
        internal static string @Arg_EnumAndObjectMustBeSameType => GetResourceString("Arg_EnumAndObjectMustBeSameType");
        /// <summary>Entry point was not found.</summary>
        internal static string @Arg_EntryPointNotFoundException => GetResourceString("Arg_EntryPointNotFoundException");
        /// <summary>Unable to find an entry point named '{0}' in DLL '{1}'.</summary>
        internal static string @Arg_EntryPointNotFoundExceptionParameterized => GetResourceString("Arg_EntryPointNotFoundExceptionParameterized");
        /// <summary>Unable to find an entry point named '{0}' in DLL.</summary>
        internal static string @Arg_EntryPointNotFoundExceptionParameterizedNoLibrary => GetResourceString("Arg_EntryPointNotFoundExceptionParameterizedNoLibrary");
        /// <summary>Illegal enum value: {0}.</summary>
        internal static string @Arg_EnumIllegalVal => GetResourceString("Arg_EnumIllegalVal");
        /// <summary>Internal error in the runtime.</summary>
        internal static string @Arg_ExecutionEngineException => GetResourceString("Arg_ExecutionEngineException");
        /// <summary>External component has thrown an exception.</summary>
        internal static string @Arg_ExternalException => GetResourceString("Arg_ExternalException");
        /// <summary>Attempted to access a field that is not accessible by the caller.</summary>
        internal static string @Arg_FieldAccessException => GetResourceString("Arg_FieldAccessException");
        /// <summary>One of the identified items was in an invalid format.</summary>
        internal static string @Arg_FormatException => GetResourceString("Arg_FormatException");
        /// <summary>Byte array for GUID must be exactly {0} bytes long.</summary>
        internal static string @Arg_GuidArrayCtor => GetResourceString("Arg_GuidArrayCtor");
        /// <summary>The number style AllowHexSpecifier is not supported on floating point data types.</summary>
        internal static string @Arg_HexStyleNotSupported => GetResourceString("Arg_HexStyleNotSupported");
        /// <summary>Hashtable's capacity overflowed and went negative. Check load factor, capacity and the current size of the table.</summary>
        internal static string @Arg_HTCapacityOverflow => GetResourceString("Arg_HTCapacityOverflow");
        /// <summary>Index was outside the bounds of the array.</summary>
        internal static string @Arg_IndexOutOfRangeException => GetResourceString("Arg_IndexOutOfRangeException");
        /// <summary>Insufficient stack to continue executing the program safely. This can happen from having too many functions on the call stack or function on the stack using too much stack space.</summary>
        internal static string @Arg_InsufficientExecutionStackException => GetResourceString("Arg_InsufficientExecutionStackException");
        /// <summary>Invalid Base.</summary>
        internal static string @Arg_InvalidBase => GetResourceString("Arg_InvalidBase");
        /// <summary>Specified cast is not valid.</summary>
        internal static string @Arg_InvalidCastException => GetResourceString("Arg_InvalidCastException");
        /// <summary>With the AllowHexSpecifier bit set in the enum bit field, the only other valid bits that can be combined into the enum value must be a subset of those in HexNumber.</summary>
        internal static string @Arg_InvalidHexStyle => GetResourceString("Arg_InvalidHexStyle");
        /// <summary>Operation is not valid due to the current state of the object.</summary>
        internal static string @Arg_InvalidOperationException => GetResourceString("Arg_InvalidOperationException");
        /// <summary>Not a legal OleAut date.</summary>
        internal static string @Arg_OleAutDateInvalid => GetResourceString("Arg_OleAutDateInvalid");
        /// <summary>OleAut date did not convert to a DateTime correctly.</summary>
        internal static string @Arg_OleAutDateScale => GetResourceString("Arg_OleAutDateScale");
        /// <summary>Invalid RuntimeTypeHandle.</summary>
        internal static string @Arg_InvalidRuntimeTypeHandle => GetResourceString("Arg_InvalidRuntimeTypeHandle");
        /// <summary>I/O error occurred.</summary>
        internal static string @Arg_IOException => GetResourceString("Arg_IOException");
        /// <summary>The given key was not present in the dictionary.</summary>
        internal static string @Arg_KeyNotFound => GetResourceString("Arg_KeyNotFound");
        /// <summary>The given key '{0}' was not present in the dictionary.</summary>
        internal static string @Arg_KeyNotFoundWithKey => GetResourceString("Arg_KeyNotFoundWithKey");
        /// <summary>Source string was not long enough. Check sourceIndex and count.</summary>
        internal static string @Arg_LongerThanSrcString => GetResourceString("Arg_LongerThanSrcString");
        /// <summary>The arrays' lower bounds must be identical.</summary>
        internal static string @Arg_LowerBoundsMustMatch => GetResourceString("Arg_LowerBoundsMustMatch");
        /// <summary>Attempted to access a non-existing field.</summary>
        internal static string @Arg_MissingFieldException => GetResourceString("Arg_MissingFieldException");
        /// <summary>Attempt to access the method failed.</summary>
        internal static string @Arg_MethodAccessException => GetResourceString("Arg_MethodAccessException");
        /// <summary>Attempted to access a missing member.</summary>
        internal static string @Arg_MissingMemberException => GetResourceString("Arg_MissingMemberException");
        /// <summary>Attempted to access a missing method.</summary>
        internal static string @Arg_MissingMethodException => GetResourceString("Arg_MissingMethodException");
        /// <summary>Attempted to add multiple callbacks to a delegate that does not support multicast.</summary>
        internal static string @Arg_MulticastNotSupportedException => GetResourceString("Arg_MulticastNotSupportedException");
        /// <summary>Object must be of type Boolean.</summary>
        internal static string @Arg_MustBeBoolean => GetResourceString("Arg_MustBeBoolean");
        /// <summary>Object must be of type Byte.</summary>
        internal static string @Arg_MustBeByte => GetResourceString("Arg_MustBeByte");
        /// <summary>Object must be of type Char.</summary>
        internal static string @Arg_MustBeChar => GetResourceString("Arg_MustBeChar");
        /// <summary>Object must be of type DateOnly.</summary>
        internal static string @Arg_MustBeDateOnly => GetResourceString("Arg_MustBeDateOnly");
        /// <summary>Object must be of type TimeOnly.</summary>
        internal static string @Arg_MustBeTimeOnly => GetResourceString("Arg_MustBeTimeOnly");
        /// <summary>Object must be of type DateTime.</summary>
        internal static string @Arg_MustBeDateTime => GetResourceString("Arg_MustBeDateTime");
        /// <summary>Object must be of type DateTimeOffset.</summary>
        internal static string @Arg_MustBeDateTimeOffset => GetResourceString("Arg_MustBeDateTimeOffset");
        /// <summary>Object must be of type Decimal.</summary>
        internal static string @Arg_MustBeDecimal => GetResourceString("Arg_MustBeDecimal");
        /// <summary>Object must be of type Double.</summary>
        internal static string @Arg_MustBeDouble => GetResourceString("Arg_MustBeDouble");
        /// <summary>Drive name must be a root directory (i.e. 'C:\') or a drive letter ('C').</summary>
        internal static string @Arg_MustBeDriveLetterOrRootDir => GetResourceString("Arg_MustBeDriveLetterOrRootDir");
        /// <summary>Type provided must be an Enum.</summary>
        internal static string @Arg_MustBeEnum => GetResourceString("Arg_MustBeEnum");
        /// <summary>Object must be of type GUID.</summary>
        internal static string @Arg_MustBeGuid => GetResourceString("Arg_MustBeGuid");
        /// <summary>Object must be of type Int16.</summary>
        internal static string @Arg_MustBeInt16 => GetResourceString("Arg_MustBeInt16");
        /// <summary>Object must be of type Int32.</summary>
        internal static string @Arg_MustBeInt32 => GetResourceString("Arg_MustBeInt32");
        /// <summary>Object must be of type Int64.</summary>
        internal static string @Arg_MustBeInt64 => GetResourceString("Arg_MustBeInt64");
        /// <summary>Object must be of type IntPtr.</summary>
        internal static string @Arg_MustBeIntPtr => GetResourceString("Arg_MustBeIntPtr");
        /// <summary>Object must be an array of primitives.</summary>
        internal static string @Arg_MustBePrimArray => GetResourceString("Arg_MustBePrimArray");
        /// <summary>Object must be of type RuntimeAssembly.</summary>
        internal static string @Arg_MustBeRuntimeAssembly => GetResourceString("Arg_MustBeRuntimeAssembly");
        /// <summary>Object must be of type SByte.</summary>
        internal static string @Arg_MustBeSByte => GetResourceString("Arg_MustBeSByte");
        /// <summary>Object must be of type Single.</summary>
        internal static string @Arg_MustBeSingle => GetResourceString("Arg_MustBeSingle");
        /// <summary>Method must be a static method.</summary>
        internal static string @Arg_MustBeStatic => GetResourceString("Arg_MustBeStatic");
        /// <summary>Object must be of type String.</summary>
        internal static string @Arg_MustBeString => GetResourceString("Arg_MustBeString");
        /// <summary>The pointer passed in as a String must not be in the bottom 64K of the process's address space.</summary>
        internal static string @Arg_MustBeStringPtrNotAtom => GetResourceString("Arg_MustBeStringPtrNotAtom");
        /// <summary>Object must be of type TimeSpan.</summary>
        internal static string @Arg_MustBeTimeSpan => GetResourceString("Arg_MustBeTimeSpan");
        /// <summary>Argument must be true.</summary>
        internal static string @Arg_MustBeTrue => GetResourceString("Arg_MustBeTrue");
        /// <summary>Object must be of type UInt16.</summary>
        internal static string @Arg_MustBeUInt16 => GetResourceString("Arg_MustBeUInt16");
        /// <summary>Object must be of type UInt32.</summary>
        internal static string @Arg_MustBeUInt32 => GetResourceString("Arg_MustBeUInt32");
        /// <summary>Object must be of type UInt64.</summary>
        internal static string @Arg_MustBeUInt64 => GetResourceString("Arg_MustBeUInt64");
        /// <summary>Object must be of type UIntPtr.</summary>
        internal static string @Arg_MustBeUIntPtr => GetResourceString("Arg_MustBeUIntPtr");
        /// <summary>Object must be of type Version.</summary>
        internal static string @Arg_MustBeVersion => GetResourceString("Arg_MustBeVersion");
        /// <summary>Must provide at least one rank.</summary>
        internal static string @Arg_NeedAtLeast1Rank => GetResourceString("Arg_NeedAtLeast1Rank");
        /// <summary>Array was not a one-dimensional array.</summary>
        internal static string @Arg_Need1DArray => GetResourceString("Arg_Need1DArray");
        /// <summary>Array was not a two-dimensional array.</summary>
        internal static string @Arg_Need2DArray => GetResourceString("Arg_Need2DArray");
        /// <summary>Array was not a three-dimensional array.</summary>
        internal static string @Arg_Need3DArray => GetResourceString("Arg_Need3DArray");
        /// <summary>Argument count must not be negative.</summary>
        internal static string @Arg_NegativeArgCount => GetResourceString("Arg_NegativeArgCount");
        /// <summary>Arg_NotFiniteNumberException = Number encountered was not a finite quantity.</summary>
        internal static string @Arg_NotFiniteNumberException => GetResourceString("Arg_NotFiniteNumberException");
        /// <summary>The lower bound of target array must be zero.</summary>
        internal static string @Arg_NonZeroLowerBound => GetResourceString("Arg_NonZeroLowerBound");
        /// <summary>Method may only be called on a Type for which Type.IsGenericParameter is true.</summary>
        internal static string @Arg_NotGenericParameter => GetResourceString("Arg_NotGenericParameter");
        /// <summary>The method or operation is not implemented.</summary>
        internal static string @Arg_NotImplementedException => GetResourceString("Arg_NotImplementedException");
        /// <summary>Specified method is not supported.</summary>
        internal static string @Arg_NotSupportedException => GetResourceString("Arg_NotSupportedException");
        /// <summary>Arrays with non-zero lower bounds are not supported.</summary>
        internal static string @Arg_NotSupportedNonZeroLowerBound => GetResourceString("Arg_NotSupportedNonZeroLowerBound");
        /// <summary>Object reference not set to an instance of an object.</summary>
        internal static string @Arg_NullReferenceException => GetResourceString("Arg_NullReferenceException");
        /// <summary>Object of type '{0}' cannot be converted to type '{1}'.</summary>
        internal static string @Arg_ObjObjEx => GetResourceString("Arg_ObjObjEx");
        /// <summary>Arithmetic operation resulted in an overflow.</summary>
        internal static string @Arg_OverflowException => GetResourceString("Arg_OverflowException");
        /// <summary>Insufficient memory to continue the execution of the program.</summary>
        internal static string @Arg_OutOfMemoryException => GetResourceString("Arg_OutOfMemoryException");
        /// <summary>Operation is not supported on this platform.</summary>
        internal static string @Arg_PlatformNotSupported => GetResourceString("Arg_PlatformNotSupported");
        /// <summary>Parameter name: {0}</summary>
        internal static string @Arg_ParamName_Name => GetResourceString("Arg_ParamName_Name");
        /// <summary>The path is empty.</summary>
        internal static string @Arg_PathEmpty => GetResourceString("Arg_PathEmpty");
        /// <summary>The UNC path '{0}' should be of the form \\\\server\\share.</summary>
        internal static string @Arg_PathIllegalUNC_Path => GetResourceString("Arg_PathIllegalUNC_Path");
        /// <summary>Attempted to operate on an array with the incorrect number of dimensions.</summary>
        internal static string @Arg_RankException => GetResourceString("Arg_RankException");
        /// <summary>Indices length does not match the array rank.</summary>
        internal static string @Arg_RankIndices => GetResourceString("Arg_RankIndices");
        /// <summary>Only single dimensional arrays are supported for the requested action.</summary>
        internal static string @Arg_RankMultiDimNotSupported => GetResourceString("Arg_RankMultiDimNotSupported");
        /// <summary>The specified arrays must have the same number of dimensions.</summary>
        internal static string @Rank_MustMatch => GetResourceString("Rank_MustMatch");
        /// <summary>Number of lengths and lowerBounds must match.</summary>
        internal static string @Arg_RanksAndBounds => GetResourceString("Arg_RanksAndBounds");
        /// <summary>RegistryKey.GetValue does not allow a String that has a length greater than Int32.MaxValue.</summary>
        internal static string @Arg_RegGetOverflowBug => GetResourceString("Arg_RegGetOverflowBug");
        /// <summary>The specified registry key does not exist.</summary>
        internal static string @Arg_RegKeyNotFound => GetResourceString("Arg_RegKeyNotFound");
        /// <summary>Specified array was not of the expected rank.</summary>
        internal static string @Arg_SafeArrayRankMismatchException => GetResourceString("Arg_SafeArrayRankMismatchException");
        /// <summary>Specified array was not of the expected type.</summary>
        internal static string @Arg_SafeArrayTypeMismatchException => GetResourceString("Arg_SafeArrayTypeMismatchException");
        /// <summary>Security error.</summary>
        internal static string @Arg_SecurityException => GetResourceString("Arg_SecurityException");
        /// <summary>Operation caused a stack overflow.</summary>
        internal static string @Arg_StackOverflowException => GetResourceString("Arg_StackOverflowException");
        /// <summary>Object synchronization method was called from an unsynchronized block of code.</summary>
        internal static string @Arg_SynchronizationLockException => GetResourceString("Arg_SynchronizationLockException");
        /// <summary>System error.</summary>
        internal static string @Arg_SystemException => GetResourceString("Arg_SystemException");
        /// <summary>Exception has been thrown by the target of an invocation.</summary>
        internal static string @Arg_TargetInvocationException => GetResourceString("Arg_TargetInvocationException");
        /// <summary>Number of parameters specified does not match the expected number.</summary>
        internal static string @Arg_TargetParameterCountException => GetResourceString("Arg_TargetParameterCountException");
        /// <summary>Missing parameter does not have a default value.</summary>
        internal static string @Arg_DefaultValueMissingException => GetResourceString("Arg_DefaultValueMissingException");
        /// <summary>Thread failed to start.</summary>
        internal static string @Arg_ThreadStartException => GetResourceString("Arg_ThreadStartException");
        /// <summary>Thread was in an invalid state for the operation being executed.</summary>
        internal static string @Arg_ThreadStateException => GetResourceString("Arg_ThreadStateException");
        /// <summary>The operation has timed out.</summary>
        internal static string @Arg_TimeoutException => GetResourceString("Arg_TimeoutException");
        /// <summary>Attempt to access the type failed.</summary>
        internal static string @Arg_TypeAccessException => GetResourceString("Arg_TypeAccessException");
        /// <summary>Failure has occurred while loading a type.</summary>
        internal static string @Arg_TypeLoadException => GetResourceString("Arg_TypeLoadException");
        /// <summary>Attempted to perform an unauthorized operation.</summary>
        internal static string @Arg_UnauthorizedAccessException => GetResourceString("Arg_UnauthorizedAccessException");
        /// <summary>Version string portion was too short or too long.</summary>
        internal static string @Arg_VersionString => GetResourceString("Arg_VersionString");
        /// <summary>The value '{0}' is not of type '{1}' and cannot be used in this generic collection.</summary>
        internal static string @Arg_WrongType => GetResourceString("Arg_WrongType");
        /// <summary>Absolute path information is required.</summary>
        internal static string @Argument_AbsolutePathRequired => GetResourceString("Argument_AbsolutePathRequired");
        /// <summary>An item with the same key has already been added.</summary>
        internal static string @Argument_AddingDuplicate => GetResourceString("Argument_AddingDuplicate");
        /// <summary>Item has already been added. Key in dictionary: '{0}'  Key being added: '{1}'</summary>
        internal static string @Argument_AddingDuplicate__ => GetResourceString("Argument_AddingDuplicate__");
        /// <summary>An item with the same key has already been added. Key: {0}</summary>
        internal static string @Argument_AddingDuplicateWithKey => GetResourceString("Argument_AddingDuplicateWithKey");
        /// <summary>The AdjustmentRule array cannot contain null elements.</summary>
        internal static string @Argument_AdjustmentRulesNoNulls => GetResourceString("Argument_AdjustmentRulesNoNulls");
        /// <summary>The elements of the AdjustmentRule array must be in chronological order and must not overlap.</summary>
        internal static string @Argument_AdjustmentRulesOutOfOrder => GetResourceString("Argument_AdjustmentRulesOutOfOrder");
        /// <summary>Attribute names must be unique.</summary>
        internal static string @Argument_AttributeNamesMustBeUnique => GetResourceString("Argument_AttributeNamesMustBeUnique");
        /// <summary>Format specifier was invalid.</summary>
        internal static string @Argument_BadFormatSpecifier => GetResourceString("Argument_BadFormatSpecifier");
        /// <summary>{0} is not a supported code page.</summary>
        internal static string @Argument_CodepageNotSupported => GetResourceString("Argument_CodepageNotSupported");
        /// <summary>CompareOption.Ordinal cannot be used with other options.</summary>
        internal static string @Argument_CompareOptionOrdinal => GetResourceString("Argument_CompareOptionOrdinal");
        /// <summary>The DateTimeStyles value RoundtripKind cannot be used with the values AssumeLocal, AssumeUniversal or AdjustToUniversal.</summary>
        internal static string @Argument_ConflictingDateTimeRoundtripStyles => GetResourceString("Argument_ConflictingDateTimeRoundtripStyles");
        /// <summary>The DateTimeStyles values AssumeLocal and AssumeUniversal cannot be used together.</summary>
        internal static string @Argument_ConflictingDateTimeStyles => GetResourceString("Argument_ConflictingDateTimeStyles");
        /// <summary>Conversion buffer overflow.</summary>
        internal static string @Argument_ConversionOverflow => GetResourceString("Argument_ConversionOverflow");
        /// <summary>The conversion could not be completed because the supplied DateTime did not have the Kind property set correctly.  For example, when the Kind property is DateTimeKind.Local, the source time zone must be TimeZoneInfo.Local.</summary>
        internal static string @Argument_ConvertMismatch => GetResourceString("Argument_ConvertMismatch");
        /// <summary>{0} is an invalid culture identifier.</summary>
        internal static string @Argument_CultureInvalidIdentifier => GetResourceString("Argument_CultureInvalidIdentifier");
        /// <summary>Culture IETF Name {0} is not a recognized IETF name.</summary>
        internal static string @Argument_CultureIetfNotSupported => GetResourceString("Argument_CultureIetfNotSupported");
        /// <summary>Culture ID {0} (0x{0:X4}) is a neutral culture; a region cannot be created from it.</summary>
        internal static string @Argument_CultureIsNeutral => GetResourceString("Argument_CultureIsNeutral");
        /// <summary>Culture is not supported.</summary>
        internal static string @Argument_CultureNotSupported => GetResourceString("Argument_CultureNotSupported");
        /// <summary>Customized cultures cannot be passed by LCID, only by name.</summary>
        internal static string @Argument_CustomCultureCannotBePassedByNumber => GetResourceString("Argument_CustomCultureCannotBePassedByNumber");
        /// <summary>The binary data must result in a DateTime with ticks between DateTime.MinValue.Ticks and DateTime.MaxValue.Ticks.</summary>
        internal static string @Argument_DateTimeBadBinaryData => GetResourceString("Argument_DateTimeBadBinaryData");
        /// <summary>The supplied DateTime must have the Year, Month, and Day properties set to 1.  The time cannot be specified more precisely than whole milliseconds.</summary>
        internal static string @Argument_DateTimeHasTicks => GetResourceString("Argument_DateTimeHasTicks");
        /// <summary>The supplied DateTime includes a TimeOfDay setting.   This is not supported.</summary>
        internal static string @Argument_DateTimeHasTimeOfDay => GetResourceString("Argument_DateTimeHasTimeOfDay");
        /// <summary>The supplied DateTime represents an invalid time.  For example, when the clock is adjusted forward, any time in the period that is skipped is invalid.</summary>
        internal static string @Argument_DateTimeIsInvalid => GetResourceString("Argument_DateTimeIsInvalid");
        /// <summary>The supplied DateTime is not in an ambiguous time range.</summary>
        internal static string @Argument_DateTimeIsNotAmbiguous => GetResourceString("Argument_DateTimeIsNotAmbiguous");
        /// <summary>The supplied DateTime must have the Kind property set to DateTimeKind.Unspecified.</summary>
        internal static string @Argument_DateTimeKindMustBeUnspecified => GetResourceString("Argument_DateTimeKindMustBeUnspecified");
        /// <summary>The supplied DateTime must have the Kind property set to DateTimeKind.Unspecified or DateTimeKind.Utc.</summary>
        internal static string @Argument_DateTimeKindMustBeUnspecifiedOrUtc => GetResourceString("Argument_DateTimeKindMustBeUnspecifiedOrUtc");
        /// <summary>The DateTimeStyles value 'NoCurrentDateDefault' is not allowed when parsing DateTimeOffset.</summary>
        internal static string @Argument_DateTimeOffsetInvalidDateTimeStyles => GetResourceString("Argument_DateTimeOffsetInvalidDateTimeStyles");
        /// <summary>The supplied DateTimeOffset is not in an ambiguous time range.</summary>
        internal static string @Argument_DateTimeOffsetIsNotAmbiguous => GetResourceString("Argument_DateTimeOffsetIsNotAmbiguous");
        /// <summary>Decimal separator cannot be the empty string.</summary>
        internal static string @Argument_EmptyDecString => GetResourceString("Argument_EmptyDecString");
        /// <summary>Empty file name is not legal.</summary>
        internal static string @Argument_EmptyFileName => GetResourceString("Argument_EmptyFileName");
        /// <summary>Empty name is not legal.</summary>
        internal static string @Argument_EmptyName => GetResourceString("Argument_EmptyName");
        /// <summary>Waithandle array may not be empty.</summary>
        internal static string @Argument_EmptyWaithandleArray => GetResourceString("Argument_EmptyWaithandleArray");
        /// <summary>Must complete Convert() operation or call Encoder.Reset() before calling GetBytes() or GetByteCount(). Encoder '{0}' fallback '{1}'.</summary>
        internal static string @Argument_EncoderFallbackNotEmpty => GetResourceString("Argument_EncoderFallbackNotEmpty");
        /// <summary>The output byte buffer is too small to contain the encoded data, encoding '{0}' fallback '{1}'.</summary>
        internal static string @Argument_EncodingConversionOverflowBytes => GetResourceString("Argument_EncodingConversionOverflowBytes");
        /// <summary>The output char buffer is too small to contain the decoded characters, encoding '{0}' fallback '{1}'.</summary>
        internal static string @Argument_EncodingConversionOverflowChars => GetResourceString("Argument_EncodingConversionOverflowChars");
        /// <summary>'{0}' is not a supported encoding name. For information on defining a custom encoding, see the documentation for the Encoding.RegisterProvider method.</summary>
        internal static string @Argument_EncodingNotSupported => GetResourceString("Argument_EncodingNotSupported");
        /// <summary>The argument type, '{0}', is not the same as the enum type '{1}'.</summary>
        internal static string @Argument_EnumTypeDoesNotMatch => GetResourceString("Argument_EnumTypeDoesNotMatch");
        /// <summary>Cannot change fallback when buffer is not empty. Previous Convert() call left data in the fallback buffer.</summary>
        internal static string @Argument_FallbackBufferNotEmpty => GetResourceString("Argument_FallbackBufferNotEmpty");
        /// <summary>At least one object must implement IComparable.</summary>
        internal static string @Argument_ImplementIComparable => GetResourceString("Argument_ImplementIComparable");
        /// <summary>Type of argument is not compatible with the generic comparer.</summary>
        internal static string @Argument_InvalidArgumentForComparison => GetResourceString("Argument_InvalidArgumentForComparison");
        /// <summary>Length of the array must be {0}.</summary>
        internal static string @Argument_InvalidArrayLength => GetResourceString("Argument_InvalidArrayLength");
        /// <summary>Target array type is not compatible with the type of items in the collection.</summary>
        internal static string @Argument_InvalidArrayType => GetResourceString("Argument_InvalidArrayType");
        /// <summary>Not a valid calendar for the given culture.</summary>
        internal static string @Argument_InvalidCalendar => GetResourceString("Argument_InvalidCalendar");
        /// <summary>Invalid Unicode code point found at index {0}.</summary>
        internal static string @Argument_InvalidCharSequence => GetResourceString("Argument_InvalidCharSequence");
        /// <summary>String contains invalid Unicode code points.</summary>
        internal static string @Argument_InvalidCharSequenceNoIndex => GetResourceString("Argument_InvalidCharSequenceNoIndex");
        /// <summary>Unable to translate bytes {0} at index {1} from specified code page to Unicode.</summary>
        internal static string @Argument_InvalidCodePageBytesIndex => GetResourceString("Argument_InvalidCodePageBytesIndex");
        /// <summary>Unable to translate Unicode character \\u{0:X4} at index {1} to specified code page.</summary>
        internal static string @Argument_InvalidCodePageConversionIndex => GetResourceString("Argument_InvalidCodePageConversionIndex");
        /// <summary>Culture name '{0}' is not supported.</summary>
        internal static string @Argument_InvalidCultureName => GetResourceString("Argument_InvalidCultureName");
        /// <summary>Culture name '{0}' is not a predefined culture.</summary>
        internal static string @Argument_InvalidPredefinedCultureName => GetResourceString("Argument_InvalidPredefinedCultureName");
        /// <summary>Invalid DateTimeKind value.</summary>
        internal static string @Argument_InvalidDateTimeKind => GetResourceString("Argument_InvalidDateTimeKind");
        /// <summary>An undefined DateTimeStyles value is being used.</summary>
        internal static string @Argument_InvalidDateTimeStyles => GetResourceString("Argument_InvalidDateTimeStyles");
        /// <summary>The only allowed values for the styles are AllowWhiteSpaces, AllowTrailingWhite, AllowLeadingWhite, and AllowInnerWhite.</summary>
        internal static string @Argument_InvalidDateStyles => GetResourceString("Argument_InvalidDateStyles");
        /// <summary>The DigitSubstitution property must be of a valid member of the DigitShapes enumeration. Valid entries include Context, NativeNational or None.</summary>
        internal static string @Argument_InvalidDigitSubstitution => GetResourceString("Argument_InvalidDigitSubstitution");
        /// <summary>Invalid element name '{0}'.</summary>
        internal static string @Argument_InvalidElementName => GetResourceString("Argument_InvalidElementName");
        /// <summary>Invalid element tag '{0}'.</summary>
        internal static string @Argument_InvalidElementTag => GetResourceString("Argument_InvalidElementTag");
        /// <summary>Invalid element text '{0}'.</summary>
        internal static string @Argument_InvalidElementText => GetResourceString("Argument_InvalidElementText");
        /// <summary>Invalid element value '{0}'.</summary>
        internal static string @Argument_InvalidElementValue => GetResourceString("Argument_InvalidElementValue");
        /// <summary>The value '{0}' is not valid for this usage of the type {1}.</summary>
        internal static string @Argument_InvalidEnumValue => GetResourceString("Argument_InvalidEnumValue");
        /// <summary>Value of flags is invalid.</summary>
        internal static string @Argument_InvalidFlag => GetResourceString("Argument_InvalidFlag");
        /// <summary>Every element in the value array should be between one and nine, except for the last element, which can be zero.</summary>
        internal static string @Argument_InvalidGroupSize => GetResourceString("Argument_InvalidGroupSize");
        /// <summary>Found a high surrogate char without a following low surrogate at index: {0}. The input may not be in this encoding, or may not contain valid Unicode (UTF-16) characters.</summary>
        internal static string @Argument_InvalidHighSurrogate => GetResourceString("Argument_InvalidHighSurrogate");
        /// <summary>The specified ID parameter '{0}' is not supported.</summary>
        internal static string @Argument_InvalidId => GetResourceString("Argument_InvalidId");
        /// <summary>Found a low surrogate char without a preceding high surrogate at index: {0}. The input may not be in this encoding, or may not contain valid Unicode (UTF-16) characters.</summary>
        internal static string @Argument_InvalidLowSurrogate => GetResourceString("Argument_InvalidLowSurrogate");
        /// <summary>The NativeDigits array must contain exactly ten members.</summary>
        internal static string @Argument_InvalidNativeDigitCount => GetResourceString("Argument_InvalidNativeDigitCount");
        /// <summary>Each member of the NativeDigits array must be a single text element (one or more UTF16 code points) with a Unicode Nd (Number, Decimal Digit) property indicating it is a digit.</summary>
        internal static string @Argument_InvalidNativeDigitValue => GetResourceString("Argument_InvalidNativeDigitValue");
        /// <summary>The region name {0} should not correspond to neutral culture; a specific culture name is required.</summary>
        internal static string @Argument_InvalidNeutralRegionName => GetResourceString("Argument_InvalidNeutralRegionName");
        /// <summary>Invalid normalization form.</summary>
        internal static string @Argument_InvalidNormalizationForm => GetResourceString("Argument_InvalidNormalizationForm");
        /// <summary>An undefined NumberStyles value is being used.</summary>
        internal static string @Argument_InvalidNumberStyles => GetResourceString("Argument_InvalidNumberStyles");
        /// <summary>Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.</summary>
        internal static string @Argument_InvalidOffLen => GetResourceString("Argument_InvalidOffLen");
        /// <summary>Illegal characters in path.</summary>
        internal static string @Argument_InvalidPathChars => GetResourceString("Argument_InvalidPathChars");
        /// <summary>The REG_TZI_FORMAT structure is corrupt.</summary>
        internal static string @Argument_InvalidREG_TZI_FORMAT => GetResourceString("Argument_InvalidREG_TZI_FORMAT");
        /// <summary>The given culture name '{0}' cannot be used to locate a resource file. Resource filenames must consist of only letters, numbers, hyphens or underscores.</summary>
        internal static string @Argument_InvalidResourceCultureName => GetResourceString("Argument_InvalidResourceCultureName");
        /// <summary>The specified serialized string '{0}' is not supported.</summary>
        internal static string @Argument_InvalidSerializedString => GetResourceString("Argument_InvalidSerializedString");
        /// <summary>An undefined TimeSpanStyles value is being used.</summary>
        internal static string @Argument_InvalidTimeSpanStyles => GetResourceString("Argument_InvalidTimeSpanStyles");
        /// <summary>Argument must be initialized to false</summary>
        internal static string @Argument_MustBeFalse => GetResourceString("Argument_MustBeFalse");
        /// <summary>Assembly must be a runtime Assembly object.</summary>
        internal static string @Argument_MustBeRuntimeAssembly => GetResourceString("Argument_MustBeRuntimeAssembly");
        /// <summary>Type must be a runtime Type object.</summary>
        internal static string @Argument_MustBeRuntimeType => GetResourceString("Argument_MustBeRuntimeType");
        /// <summary>The specified structure '{0}' must be blittable or have layout information.</summary>
        internal static string @Argument_MustHaveLayoutOrBeBlittable => GetResourceString("Argument_MustHaveLayoutOrBeBlittable");
        /// <summary>The specified object must not be an instance of a generic type.</summary>
        internal static string @Argument_NeedNonGenericObject => GetResourceString("Argument_NeedNonGenericObject");
        /// <summary>The specified Type must not be a generic type definition.</summary>
        internal static string @Argument_NeedNonGenericType => GetResourceString("Argument_NeedNonGenericType");
        /// <summary>No Era was supplied.</summary>
        internal static string @Argument_NoEra => GetResourceString("Argument_NoEra");
        /// <summary>There is no region associated with the Invariant Culture (Culture ID: 0x7F).</summary>
        internal static string @Argument_NoRegionInvariantCulture => GetResourceString("Argument_NoRegionInvariantCulture");
        /// <summary>Object contains non-primitive or non-blittable data.</summary>
        internal static string @ArgumentException_NotIsomorphic => GetResourceString("ArgumentException_NotIsomorphic");
        /// <summary>Path cannot be the empty string or all whitespace.</summary>
        internal static string @Argument_PathEmpty => GetResourceString("Argument_PathEmpty");
        /// <summary>Array size exceeds addressing limitations.</summary>
        internal static string @Argument_StructArrayTooLarge => GetResourceString("Argument_StructArrayTooLarge");
        /// <summary>The UTC Offset of the local dateTime parameter does not match the offset argument.</summary>
        internal static string @Argument_OffsetLocalMismatch => GetResourceString("Argument_OffsetLocalMismatch");
        /// <summary>Offset must be specified in whole minutes.</summary>
        internal static string @Argument_OffsetPrecision => GetResourceString("Argument_OffsetPrecision");
        /// <summary>Offset must be within plus or minus 14 hours.</summary>
        internal static string @Argument_OffsetOutOfRange => GetResourceString("Argument_OffsetOutOfRange");
        /// <summary>The UTC Offset for Utc DateTime instances must be 0.</summary>
        internal static string @Argument_OffsetUtcMismatch => GetResourceString("Argument_OffsetUtcMismatch");
        /// <summary>Culture name {0} or {1} is not supported.</summary>
        internal static string @Argument_OneOfCulturesNotSupported => GetResourceString("Argument_OneOfCulturesNotSupported");
        /// <summary>Only mscorlib's assembly is valid.</summary>
        internal static string @Argument_OnlyMscorlib => GetResourceString("Argument_OnlyMscorlib");
        /// <summary>The DateStart property must come before the DateEnd property.</summary>
        internal static string @Argument_OutOfOrderDateTimes => GetResourceString("Argument_OutOfOrderDateTimes");
        /// <summary>Number was less than the array's lower bound in the first dimension.</summary>
        internal static string @ArgumentOutOfRange_ArrayLB => GetResourceString("ArgumentOutOfRange_ArrayLB");
        /// <summary>Arrays larger than 2GB are not supported.</summary>
        internal static string @ArgumentOutOfRange_HugeArrayNotSupported => GetResourceString("ArgumentOutOfRange_HugeArrayNotSupported");
        /// <summary>Index was out of range. Must be non-negative and less than the size of the collection.</summary>
        internal static string @ArgumentOutOfRange_Index => GetResourceString("ArgumentOutOfRange_Index");
        /// <summary>The specified length exceeds maximum capacity of SecureString.</summary>
        internal static string @ArgumentOutOfRange_Length => GetResourceString("ArgumentOutOfRange_Length");
        /// <summary>The specified length exceeds the maximum value of {0}.</summary>
        internal static string @ArgumentOutOfRange_LengthTooLarge => GetResourceString("ArgumentOutOfRange_LengthTooLarge");
        /// <summary>Argument must be less than or equal to 2^31 - 1 milliseconds.</summary>
        internal static string @ArgumentOutOfRange_LessEqualToIntegerMaxVal => GetResourceString("ArgumentOutOfRange_LessEqualToIntegerMaxVal");
        /// <summary>Non-negative number required.</summary>
        internal static string @ArgumentOutOfRange_NeedNonNegNum => GetResourceString("ArgumentOutOfRange_NeedNonNegNum");
        /// <summary>The ID parameter must be in the range {0} through {1}.</summary>
        internal static string @ArgumentOutOfRange_NeedValidId => GetResourceString("ArgumentOutOfRange_NeedValidId");
        /// <summary>The name of the type is invalid.</summary>
        internal static string @Argument_InvalidTypeName => GetResourceString("Argument_InvalidTypeName");
        /// <summary>The format of the path '{0}' is not supported.</summary>
        internal static string @Argument_PathFormatNotSupported_Path => GetResourceString("Argument_PathFormatNotSupported_Path");
        /// <summary>Recursive fallback not allowed for character \\u{0:X4}.</summary>
        internal static string @Argument_RecursiveFallback => GetResourceString("Argument_RecursiveFallback");
        /// <summary>Recursive fallback not allowed for bytes {0}.</summary>
        internal static string @Argument_RecursiveFallbackBytes => GetResourceString("Argument_RecursiveFallbackBytes");
        /// <summary>The result is out of the supported range for this calendar. The result should be between {0} (Gregorian date) and {1} (Gregorian date), inclusive.</summary>
        internal static string @Argument_ResultCalendarRange => GetResourceString("Argument_ResultCalendarRange");
        /// <summary>The initial count for the semaphore must be greater than or equal to zero and less than the maximum count.</summary>
        internal static string @Argument_SemaphoreInitialMaximum => GetResourceString("Argument_SemaphoreInitialMaximum");
        /// <summary>The structure must not be a value class.</summary>
        internal static string @Argument_StructMustNotBeValueClass => GetResourceString("Argument_StructMustNotBeValueClass");
        /// <summary>The TimeSpan parameter cannot be specified more precisely than whole minutes.</summary>
        internal static string @Argument_TimeSpanHasSeconds => GetResourceString("Argument_TimeSpanHasSeconds");
        /// <summary>The time zone ID '{0}' was not found on the local computer.</summary>
        internal static string @Argument_TimeZoneNotFound => GetResourceString("Argument_TimeZoneNotFound");
        /// <summary>The tzfile does not begin with the magic characters 'TZif'.  Please verify that the file is not corrupt.</summary>
        internal static string @Argument_TimeZoneInfoBadTZif => GetResourceString("Argument_TimeZoneInfoBadTZif");
        /// <summary>The TZif data structure is corrupt.</summary>
        internal static string @Argument_TimeZoneInfoInvalidTZif => GetResourceString("Argument_TimeZoneInfoInvalidTZif");
        /// <summary>fromInclusive must be less than or equal to toExclusive.</summary>
        internal static string @Argument_ToExclusiveLessThanFromExclusive => GetResourceString("Argument_ToExclusiveLessThanFromExclusive");
        /// <summary>The DaylightTransitionStart property must not equal the DaylightTransitionEnd property.</summary>
        internal static string @Argument_TransitionTimesAreIdentical => GetResourceString("Argument_TransitionTimesAreIdentical");
        /// <summary>The type must not be imported from COM.</summary>
        internal static string @Argument_TypeMustNotBeComImport => GetResourceString("Argument_TypeMustNotBeComImport");
        /// <summary>The UTC time represented when the offset is applied must be between year 0 and 10,000.</summary>
        internal static string @Argument_UTCOutOfRange => GetResourceString("Argument_UTCOutOfRange");
        /// <summary>The name can be no more than {0} characters in length.</summary>
        internal static string @Argument_WaitHandleNameTooLong => GetResourceString("Argument_WaitHandleNameTooLong");
        /// <summary>Object is not a array with the same number of elements as the array to compare it to.</summary>
        internal static string @ArgumentException_OtherNotArrayOfCorrectLength => GetResourceString("ArgumentException_OtherNotArrayOfCorrectLength");
        /// <summary>Argument must be of type {0}.</summary>
        internal static string @ArgumentException_TupleIncorrectType => GetResourceString("ArgumentException_TupleIncorrectType");
        /// <summary>The last element of an eight element tuple must be a Tuple.</summary>
        internal static string @ArgumentException_TupleLastArgumentNotATuple => GetResourceString("ArgumentException_TupleLastArgumentNotATuple");
        /// <summary>Argument must be of type {0}.</summary>
        internal static string @ArgumentException_ValueTupleIncorrectType => GetResourceString("ArgumentException_ValueTupleIncorrectType");
        /// <summary>The last element of an eight element ValueTuple must be a ValueTuple.</summary>
        internal static string @ArgumentException_ValueTupleLastArgumentNotAValueTuple => GetResourceString("ArgumentException_ValueTupleLastArgumentNotAValueTuple");
        /// <summary>Array cannot be null.</summary>
        internal static string @ArgumentNull_Array => GetResourceString("ArgumentNull_Array");
        /// <summary>At least one element in the specified array was null.</summary>
        internal static string @ArgumentNull_ArrayElement => GetResourceString("ArgumentNull_ArrayElement");
        /// <summary>Found a null value within an array.</summary>
        internal static string @ArgumentNull_ArrayValue => GetResourceString("ArgumentNull_ArrayValue");
        /// <summary>Cannot have a null child.</summary>
        internal static string @ArgumentNull_Child => GetResourceString("ArgumentNull_Child");
        /// <summary>Collection cannot be null.</summary>
        internal static string @ArgumentNull_Collection => GetResourceString("ArgumentNull_Collection");
        /// <summary>Dictionary cannot be null.</summary>
        internal static string @ArgumentNull_Dictionary => GetResourceString("ArgumentNull_Dictionary");
        /// <summary>File name cannot be null.</summary>
        internal static string @ArgumentNull_FileName => GetResourceString("ArgumentNull_FileName");
        /// <summary>Value cannot be null.</summary>
        internal static string @ArgumentNull_Generic => GetResourceString("ArgumentNull_Generic");
        /// <summary>Key cannot be null.</summary>
        internal static string @ArgumentNull_Key => GetResourceString("ArgumentNull_Key");
        /// <summary>String reference not set to an instance of a String.</summary>
        internal static string @ArgumentNull_String => GetResourceString("ArgumentNull_String");
        /// <summary>Type cannot be null.</summary>
        internal static string @ArgumentNull_Type => GetResourceString("ArgumentNull_Type");
        /// <summary>The waitHandles parameter cannot be null.</summary>
        internal static string @ArgumentNull_Waithandles => GetResourceString("ArgumentNull_Waithandles");
        /// <summary>Value to add was out of range.</summary>
        internal static string @ArgumentOutOfRange_AddValue => GetResourceString("ArgumentOutOfRange_AddValue");
        /// <summary>Actual value was {0}.</summary>
        internal static string @ArgumentOutOfRange_ActualValue => GetResourceString("ArgumentOutOfRange_ActualValue");
        /// <summary>Year, Month, and Day parameters describe an un-representable DateTime.</summary>
        internal static string @ArgumentOutOfRange_BadYearMonthDay => GetResourceString("ArgumentOutOfRange_BadYearMonthDay");
        /// <summary>Hour, Minute, and Second parameters describe an un-representable DateTime.</summary>
        internal static string @ArgumentOutOfRange_BadHourMinuteSecond => GetResourceString("ArgumentOutOfRange_BadHourMinuteSecond");
        /// <summary>Must be less than or equal to the size of the collection.</summary>
        internal static string @ArgumentOutOfRange_BiggerThanCollection => GetResourceString("ArgumentOutOfRange_BiggerThanCollection");
        /// <summary>Argument must be between {0} and {1}.</summary>
        internal static string @ArgumentOutOfRange_Bounds_Lower_Upper => GetResourceString("ArgumentOutOfRange_Bounds_Lower_Upper");
        /// <summary>Specified time is not supported in this calendar. It should be between {0} (Gregorian date) and {1} (Gregorian date), inclusive.</summary>
        internal static string @ArgumentOutOfRange_CalendarRange => GetResourceString("ArgumentOutOfRange_CalendarRange");
        /// <summary>Capacity exceeds maximum capacity.</summary>
        internal static string @ArgumentOutOfRange_Capacity => GetResourceString("ArgumentOutOfRange_Capacity");
        /// <summary>Count must be positive and count must refer to a location within the string/array/collection.</summary>
        internal static string @ArgumentOutOfRange_Count => GetResourceString("ArgumentOutOfRange_Count");
        /// <summary>The added or subtracted value results in an un-representable DateTime.</summary>
        internal static string @ArgumentOutOfRange_DateArithmetic => GetResourceString("ArgumentOutOfRange_DateArithmetic");
        /// <summary>Months value must be between +/-120000.</summary>
        internal static string @ArgumentOutOfRange_DateTimeBadMonths => GetResourceString("ArgumentOutOfRange_DateTimeBadMonths");
        /// <summary>Ticks must be between DateTime.MinValue.Ticks and DateTime.MaxValue.Ticks.</summary>
        internal static string @ArgumentOutOfRange_DateTimeBadTicks => GetResourceString("ArgumentOutOfRange_DateTimeBadTicks");
        /// <summary>Ticks must be between 0 and and TimeOnly.MaxValue.Ticks.</summary>
        internal static string @ArgumentOutOfRange_TimeOnlyBadTicks => GetResourceString("ArgumentOutOfRange_TimeOnlyBadTicks");
        /// <summary>Years value must be between +/-10000.</summary>
        internal static string @ArgumentOutOfRange_DateTimeBadYears => GetResourceString("ArgumentOutOfRange_DateTimeBadYears");
        /// <summary>Day must be between 1 and {0} for month {1}.</summary>
        internal static string @ArgumentOutOfRange_Day => GetResourceString("ArgumentOutOfRange_Day");
        /// <summary>The DayOfWeek enumeration must be in the range 0 through 6.</summary>
        internal static string @ArgumentOutOfRange_DayOfWeek => GetResourceString("ArgumentOutOfRange_DayOfWeek");
        /// <summary>The Day parameter must be in the range 1 through 31.</summary>
        internal static string @ArgumentOutOfRange_DayParam => GetResourceString("ArgumentOutOfRange_DayParam");
        /// <summary>Decimal can only round to between 0 and 28 digits of precision.</summary>
        internal static string @ArgumentOutOfRange_DecimalRound => GetResourceString("ArgumentOutOfRange_DecimalRound");
        /// <summary>Decimal's scale value must be between 0 and 28, inclusive.</summary>
        internal static string @ArgumentOutOfRange_DecimalScale => GetResourceString("ArgumentOutOfRange_DecimalScale");
        /// <summary>endIndex cannot be greater than startIndex.</summary>
        internal static string @ArgumentOutOfRange_EndIndexStartIndex => GetResourceString("ArgumentOutOfRange_EndIndexStartIndex");
        /// <summary>Enum value was out of legal range.</summary>
        internal static string @ArgumentOutOfRange_Enum => GetResourceString("ArgumentOutOfRange_Enum");
        /// <summary>Time value was out of era range.</summary>
        internal static string @ArgumentOutOfRange_Era => GetResourceString("ArgumentOutOfRange_Era");
        /// <summary>Not a valid Win32 FileTime.</summary>
        internal static string @ArgumentOutOfRange_FileTimeInvalid => GetResourceString("ArgumentOutOfRange_FileTimeInvalid");
        /// <summary>Value must be positive.</summary>
        internal static string @ArgumentOutOfRange_GenericPositive => GetResourceString("ArgumentOutOfRange_GenericPositive");
        /// <summary>Too many characters. The resulting number of bytes is larger than what can be returned as an int.</summary>
        internal static string @ArgumentOutOfRange_GetByteCountOverflow => GetResourceString("ArgumentOutOfRange_GetByteCountOverflow");
        /// <summary>Too many bytes. The resulting number of chars is larger than what can be returned as an int.</summary>
        internal static string @ArgumentOutOfRange_GetCharCountOverflow => GetResourceString("ArgumentOutOfRange_GetCharCountOverflow");
        /// <summary>Load factor needs to be between 0.1 and 1.0.</summary>
        internal static string @ArgumentOutOfRange_HashtableLoadFactor => GetResourceString("ArgumentOutOfRange_HashtableLoadFactor");
        /// <summary>Index and count must refer to a location within the string.</summary>
        internal static string @ArgumentOutOfRange_IndexCount => GetResourceString("ArgumentOutOfRange_IndexCount");
        /// <summary>Index and count must refer to a location within the buffer.</summary>
        internal static string @ArgumentOutOfRange_IndexCountBuffer => GetResourceString("ArgumentOutOfRange_IndexCountBuffer");
        /// <summary>Index and length must refer to a location within the string.</summary>
        internal static string @ArgumentOutOfRange_IndexLength => GetResourceString("ArgumentOutOfRange_IndexLength");
        /// <summary>Index was out of range. Must be non-negative and less than the length of the string.</summary>
        internal static string @ArgumentOutOfRange_IndexString => GetResourceString("ArgumentOutOfRange_IndexString");
        /// <summary>Input is too large to be processed.</summary>
        internal static string @ArgumentOutOfRange_InputTooLarge => GetResourceString("ArgumentOutOfRange_InputTooLarge");
        /// <summary>Era value was not valid.</summary>
        internal static string @ArgumentOutOfRange_InvalidEraValue => GetResourceString("ArgumentOutOfRange_InvalidEraValue");
        /// <summary>A valid high surrogate character is between 0xd800 and 0xdbff, inclusive.</summary>
        internal static string @ArgumentOutOfRange_InvalidHighSurrogate => GetResourceString("ArgumentOutOfRange_InvalidHighSurrogate");
        /// <summary>A valid low surrogate character is between 0xdc00 and 0xdfff, inclusive.</summary>
        internal static string @ArgumentOutOfRange_InvalidLowSurrogate => GetResourceString("ArgumentOutOfRange_InvalidLowSurrogate");
        /// <summary>A valid UTF32 value is between 0x000000 and 0x10ffff, inclusive, and should not include surrogate codepoint values (0x00d800 ~ 0x00dfff).</summary>
        internal static string @ArgumentOutOfRange_InvalidUTF32 => GetResourceString("ArgumentOutOfRange_InvalidUTF32");
        /// <summary>The length cannot be greater than the capacity.</summary>
        internal static string @ArgumentOutOfRange_LengthGreaterThanCapacity => GetResourceString("ArgumentOutOfRange_LengthGreaterThanCapacity");
        /// <summary>Index must be within the bounds of the List.</summary>
        internal static string @ArgumentOutOfRange_ListInsert => GetResourceString("ArgumentOutOfRange_ListInsert");
        /// <summary>Index was out of range. Must be non-negative and less than the size of the list.</summary>
        internal static string @ArgumentOutOfRange_ListItem => GetResourceString("ArgumentOutOfRange_ListItem");
        /// <summary>Index was out of range. Must be non-negative and less than the size of the list.</summary>
        internal static string @ArgumentOutOfRange_ListRemoveAt => GetResourceString("ArgumentOutOfRange_ListRemoveAt");
        /// <summary>Month must be between one and twelve.</summary>
        internal static string @ArgumentOutOfRange_Month => GetResourceString("ArgumentOutOfRange_Month");
        /// <summary>Day number must be between 0 and DateOnly.MaxValue.DayNumber.</summary>
        internal static string @ArgumentOutOfRange_DayNumber => GetResourceString("ArgumentOutOfRange_DayNumber");
        /// <summary>The Month parameter must be in the range 1 through 12.</summary>
        internal static string @ArgumentOutOfRange_MonthParam => GetResourceString("ArgumentOutOfRange_MonthParam");
        /// <summary>Value must be non-negative and less than or equal to Int32.MaxValue.</summary>
        internal static string @ArgumentOutOfRange_MustBeNonNegInt32 => GetResourceString("ArgumentOutOfRange_MustBeNonNegInt32");
        /// <summary>'{0}' must be non-negative.</summary>
        internal static string @ArgumentOutOfRange_MustBeNonNegNum => GetResourceString("ArgumentOutOfRange_MustBeNonNegNum");
        /// <summary>'{0}' must be greater than zero.</summary>
        internal static string @ArgumentOutOfRange_MustBePositive => GetResourceString("ArgumentOutOfRange_MustBePositive");
        /// <summary>Number must be either non-negative and less than or equal to Int32.MaxValue or -1.</summary>
        internal static string @ArgumentOutOfRange_NeedNonNegOrNegative1 => GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1");
        /// <summary>Positive number required.</summary>
        internal static string @ArgumentOutOfRange_NeedPosNum => GetResourceString("ArgumentOutOfRange_NeedPosNum");
        /// <summary>Capacity must be positive.</summary>
        internal static string @ArgumentOutOfRange_NegativeCapacity => GetResourceString("ArgumentOutOfRange_NegativeCapacity");
        /// <summary>Count cannot be less than zero.</summary>
        internal static string @ArgumentOutOfRange_NegativeCount => GetResourceString("ArgumentOutOfRange_NegativeCount");
        /// <summary>Length cannot be less than zero.</summary>
        internal static string @ArgumentOutOfRange_NegativeLength => GetResourceString("ArgumentOutOfRange_NegativeLength");
        /// <summary>lohSize can't be greater than totalSize</summary>
        internal static string @ArgumentOutOfRange_NoGCLohSizeGreaterTotalSize => GetResourceString("ArgumentOutOfRange_NoGCLohSizeGreaterTotalSize");
        /// <summary>Offset and length must refer to a position in the string.</summary>
        internal static string @ArgumentOutOfRange_OffsetLength => GetResourceString("ArgumentOutOfRange_OffsetLength");
        /// <summary>Either offset did not refer to a position in the string, or there is an insufficient length of destination character array.</summary>
        internal static string @ArgumentOutOfRange_OffsetOut => GetResourceString("ArgumentOutOfRange_OffsetOut");
        /// <summary>Pointer startIndex and length do not refer to a valid string.</summary>
        internal static string @ArgumentOutOfRange_PartialWCHAR => GetResourceString("ArgumentOutOfRange_PartialWCHAR");
        /// <summary>Valid values are between {0} and {1}, inclusive.</summary>
        internal static string @ArgumentOutOfRange_Range => GetResourceString("ArgumentOutOfRange_Range");
        /// <summary>Rounding digits must be between 0 and 15, inclusive.</summary>
        internal static string @ArgumentOutOfRange_RoundingDigits => GetResourceString("ArgumentOutOfRange_RoundingDigits");
        /// <summary>Rounding digits must be between 0 and 6, inclusive.</summary>
        internal static string @ArgumentOutOfRange_RoundingDigits_MathF => GetResourceString("ArgumentOutOfRange_RoundingDigits_MathF");
        /// <summary>capacity was less than the current size.</summary>
        internal static string @ArgumentOutOfRange_SmallCapacity => GetResourceString("ArgumentOutOfRange_SmallCapacity");
        /// <summary>MaxCapacity must be one or greater.</summary>
        internal static string @ArgumentOutOfRange_SmallMaxCapacity => GetResourceString("ArgumentOutOfRange_SmallMaxCapacity");
        /// <summary>StartIndex cannot be less than zero.</summary>
        internal static string @ArgumentOutOfRange_StartIndex => GetResourceString("ArgumentOutOfRange_StartIndex");
        /// <summary>startIndex cannot be larger than length of string.</summary>
        internal static string @ArgumentOutOfRange_StartIndexLargerThanLength => GetResourceString("ArgumentOutOfRange_StartIndexLargerThanLength");
        /// <summary>startIndex must be less than length of string.</summary>
        internal static string @ArgumentOutOfRange_StartIndexLessThanLength => GetResourceString("ArgumentOutOfRange_StartIndexLessThanLength");
        /// <summary>The TimeSpan parameter must be within plus or minus 14.0 hours.</summary>
        internal static string @ArgumentOutOfRange_UtcOffset => GetResourceString("ArgumentOutOfRange_UtcOffset");
        /// <summary>The sum of the BaseUtcOffset and DaylightDelta properties must within plus or minus 14.0 hours.</summary>
        internal static string @ArgumentOutOfRange_UtcOffsetAndDaylightDelta => GetResourceString("ArgumentOutOfRange_UtcOffsetAndDaylightDelta");
        /// <summary>Version's parameters must be greater than or equal to zero.</summary>
        internal static string @ArgumentOutOfRange_Version => GetResourceString("ArgumentOutOfRange_Version");
        /// <summary>The Week parameter must be in the range 1 through 5.</summary>
        internal static string @ArgumentOutOfRange_Week => GetResourceString("ArgumentOutOfRange_Week");
        /// <summary>Year must be between 1 and 9999.</summary>
        internal static string @ArgumentOutOfRange_Year => GetResourceString("ArgumentOutOfRange_Year");
        /// <summary>Function does not accept floating point Not-a-Number values.</summary>
        internal static string @Arithmetic_NaN => GetResourceString("Arithmetic_NaN");
        /// <summary>Source array type cannot be assigned to destination array type.</summary>
        internal static string @ArrayTypeMismatch_CantAssignType => GetResourceString("ArrayTypeMismatch_CantAssignType");
        /// <summary>Cannot unload non-collectible AssemblyLoadContext.</summary>
        internal static string @AssemblyLoadContext_Unload_CannotUnloadIfNotCollectible => GetResourceString("AssemblyLoadContext_Unload_CannotUnloadIfNotCollectible");
        /// <summary>Unload called on AssemblyLoadContext that is unloading or that was already unloaded.</summary>
        internal static string @AssemblyLoadContext_Unload_AlreadyUnloaded => GetResourceString("AssemblyLoadContext_Unload_AlreadyUnloaded");
        /// <summary>AssemblyLoadContext is unloading or was already unloaded.</summary>
        internal static string @AssemblyLoadContext_Verify_NotUnloading => GetResourceString("AssemblyLoadContext_Verify_NotUnloading");
        /// <summary>Could not load file or assembly '{0}'. An attempt was made to load a program with an incorrect format.</summary>
        internal static string @BadImageFormatException_CouldNotLoadFileOrAssembly => GetResourceString("BadImageFormatException_CouldNotLoadFileOrAssembly");
        /// <summary>A resolver is already set for the assembly.</summary>
        internal static string @InvalidOperation_CannotRegisterSecondResolver => GetResourceString("InvalidOperation_CannotRegisterSecondResolver");
        /// <summary>A prior operation on this collection was interrupted by an exception. Collection's state is no longer trusted.</summary>
        internal static string @InvalidOperation_CollectionCorrupted => GetResourceString("InvalidOperation_CollectionCorrupted");
        /// <summary>This range in the underlying list is invalid. A possible cause is that elements were removed.</summary>
        internal static string @InvalidOperation_UnderlyingArrayListChanged => GetResourceString("InvalidOperation_UnderlyingArrayListChanged");
        /// <summary>--- End of inner exception stack trace ---</summary>
        internal static string @Exception_EndOfInnerExceptionStack => GetResourceString("Exception_EndOfInnerExceptionStack");
        /// <summary>--- End of stack trace from previous location ---</summary>
        internal static string @Exception_EndStackTraceFromPreviousThrow => GetResourceString("Exception_EndStackTraceFromPreviousThrow");
        /// <summary>Exception of type '{0}' was thrown.</summary>
        internal static string @Exception_WasThrown => GetResourceString("Exception_WasThrown");
        /// <summary>The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.</summary>
        internal static string @Format_BadBase64Char => GetResourceString("Format_BadBase64Char");
        /// <summary>Invalid length for a Base-64 char array or string.</summary>
        internal static string @Format_BadBase64CharArrayLength => GetResourceString("Format_BadBase64CharArrayLength");
        /// <summary>String was not recognized as a valid Boolean.</summary>
        internal static string @Format_BadBoolean => GetResourceString("Format_BadBoolean");
        /// <summary>Format specifier '{0}' was invalid.</summary>
        internal static string @Format_BadFormatSpecifier => GetResourceString("Format_BadFormatSpecifier");
        /// <summary>No format specifiers were provided.</summary>
        internal static string @Format_NoFormatSpecifier => GetResourceString("Format_NoFormatSpecifier");
        /// <summary>The input is not a valid hex string as it contains a non-hex character.</summary>
        internal static string @Format_BadHexChar => GetResourceString("Format_BadHexChar");
        /// <summary>The input is not a valid hex string as its length is not a multiple of 2.</summary>
        internal static string @Format_BadHexLength => GetResourceString("Format_BadHexLength");
        /// <summary>Cannot find a matching quote character for the character '{0}'.</summary>
        internal static string @Format_BadQuote => GetResourceString("Format_BadQuote");
        /// <summary>Input string was either empty or contained only whitespace.</summary>
        internal static string @Format_EmptyInputString => GetResourceString("Format_EmptyInputString");
        /// <summary>Expected 0x prefix.</summary>
        internal static string @Format_GuidHexPrefix => GetResourceString("Format_GuidHexPrefix");
        /// <summary>Guid should contain 32 digits with 4 dashes (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).</summary>
        internal static string @Format_GuidInvLen => GetResourceString("Format_GuidInvLen");
        /// <summary>Guid string should only contain hexadecimal characters.</summary>
        internal static string @Format_GuidInvalidChar => GetResourceString("Format_GuidInvalidChar");
        /// <summary>Expected {0xdddddddd, etc}.</summary>
        internal static string @Format_GuidBrace => GetResourceString("Format_GuidBrace");
        /// <summary>Could not find a comma, or the length between the previous token and the comma was zero (i.e., '0x,'etc.).</summary>
        internal static string @Format_GuidComma => GetResourceString("Format_GuidComma");
        /// <summary>Could not find a brace, or the length between the previous token and the brace was zero (i.e., '0x,'etc.).</summary>
        internal static string @Format_GuidBraceAfterLastNumber => GetResourceString("Format_GuidBraceAfterLastNumber");
        /// <summary>Dashes are in the wrong position for GUID parsing.</summary>
        internal static string @Format_GuidDashes => GetResourceString("Format_GuidDashes");
        /// <summary>Could not find the ending brace.</summary>
        internal static string @Format_GuidEndBrace => GetResourceString("Format_GuidEndBrace");
        /// <summary>Additional non-parsable characters are at the end of the string.</summary>
        internal static string @Format_ExtraJunkAtEnd => GetResourceString("Format_ExtraJunkAtEnd");
        /// <summary>Unrecognized Guid format.</summary>
        internal static string @Format_GuidUnrecognized => GetResourceString("Format_GuidUnrecognized");
        /// <summary>Index (zero based) must be greater than or equal to zero and less than the size of the argument list.</summary>
        internal static string @Format_IndexOutOfRange => GetResourceString("Format_IndexOutOfRange");
        /// <summary>Format String can be only 'D', 'd', 'N', 'n', 'P', 'p', 'B', 'b', 'X' or 'x'.</summary>
        internal static string @Format_InvalidGuidFormatSpecification => GetResourceString("Format_InvalidGuidFormatSpecification");
        /// <summary>Input string was not in a correct format.</summary>
        internal static string @Format_InvalidString => GetResourceString("Format_InvalidString");
        /// <summary>String must be exactly one character long.</summary>
        internal static string @Format_NeedSingleChar => GetResourceString("Format_NeedSingleChar");
        /// <summary>Could not find any recognizable digits.</summary>
        internal static string @Format_NoParsibleDigits => GetResourceString("Format_NoParsibleDigits");
        /// <summary>String was not recognized as a valid TimeSpan.</summary>
        internal static string @Format_BadTimeSpan => GetResourceString("Format_BadTimeSpan");
        /// <summary>Insufficient available memory to meet the expected demands of an operation at this time.  Please try again later.</summary>
        internal static string @InsufficientMemory_MemFailPoint => GetResourceString("InsufficientMemory_MemFailPoint");
        /// <summary>Insufficient memory to meet the expected demands of an operation, and this system is likely to never satisfy this request.  If this is a 32 bit system, consider booting in 3 GB mode.</summary>
        internal static string @InsufficientMemory_MemFailPoint_TooBig => GetResourceString("InsufficientMemory_MemFailPoint_TooBig");
        /// <summary>Insufficient available memory to meet the expected demands of an operation at this time, possibly due to virtual address space fragmentation.  Please try again later.</summary>
        internal static string @InsufficientMemory_MemFailPoint_VAFrag => GetResourceString("InsufficientMemory_MemFailPoint_VAFrag");
        /// <summary>Cannot marshal: Encountered unmappable character.</summary>
        internal static string @Interop_Marshal_Unmappable_Char => GetResourceString("Interop_Marshal_Unmappable_Char");
        /// <summary>Null object cannot be converted to a value type.</summary>
        internal static string @InvalidCast_CannotCastNullToValueType => GetResourceString("InvalidCast_CannotCastNullToValueType");
        /// <summary>At least one element in the source array could not be cast down to the destination array type.</summary>
        internal static string @InvalidCast_DownCastArrayElement => GetResourceString("InvalidCast_DownCastArrayElement");
        /// <summary>Invalid cast from '{0}' to '{1}'.</summary>
        internal static string @InvalidCast_FromTo => GetResourceString("InvalidCast_FromTo");
        /// <summary>Object must implement IConvertible.</summary>
        internal static string @InvalidCast_IConvertible => GetResourceString("InvalidCast_IConvertible");
        /// <summary>Object cannot be stored in an array of this type.</summary>
        internal static string @InvalidCast_StoreArrayElement => GetResourceString("InvalidCast_StoreArrayElement");
        /// <summary>WinRT Interop has already been initialized and cannot be initialized again.</summary>
        internal static string @InvalidOperation_Calling => GetResourceString("InvalidOperation_Calling");
        /// <summary>Internal Error in DateTime and Calendar operations.</summary>
        internal static string @InvalidOperation_DateTimeParsing => GetResourceString("InvalidOperation_DateTimeParsing");
        /// <summary>Enumeration already finished.</summary>
        internal static string @InvalidOperation_EnumEnded => GetResourceString("InvalidOperation_EnumEnded");
        /// <summary>Collection was modified; enumeration operation may not execute.</summary>
        internal static string @InvalidOperation_EnumFailedVersion => GetResourceString("InvalidOperation_EnumFailedVersion");
        /// <summary>Enumeration has not started. Call MoveNext.</summary>
        internal static string @InvalidOperation_EnumNotStarted => GetResourceString("InvalidOperation_EnumNotStarted");
        /// <summary>Enumeration has either not started or has already finished.</summary>
        internal static string @InvalidOperation_EnumOpCantHappen => GetResourceString("InvalidOperation_EnumOpCantHappen");
        /// <summary>OSVersion's call to GetVersionEx failed.</summary>
        internal static string @InvalidOperation_GetVersion => GetResourceString("InvalidOperation_GetVersion");
        /// <summary>Handle is not initialized.</summary>
        internal static string @InvalidOperation_HandleIsNotInitialized => GetResourceString("InvalidOperation_HandleIsNotInitialized");
        /// <summary>Handle is not pinned.</summary>
        internal static string @InvalidOperation_HandleIsNotPinned => GetResourceString("InvalidOperation_HandleIsNotPinned");
        /// <summary>Hashtable insert failed. Load factor too high. The most common cause is multiple threads writing to the Hashtable simultaneously.</summary>
        internal static string @InvalidOperation_HashInsertFailed => GetResourceString("InvalidOperation_HashInsertFailed");
        /// <summary>Failed to compare two elements in the array.</summary>
        internal static string @InvalidOperation_IComparerFailed => GetResourceString("InvalidOperation_IComparerFailed");
        /// <summary>Nullable object must have a value.</summary>
        internal static string @InvalidOperation_NoValue => GetResourceString("InvalidOperation_NoValue");
        /// <summary>The underlying array is null.</summary>
        internal static string @InvalidOperation_NullArray => GetResourceString("InvalidOperation_NullArray");
        /// <summary>Cannot pack a packed Overlapped again.</summary>
        internal static string @InvalidOperation_Overlapped_Pack => GetResourceString("InvalidOperation_Overlapped_Pack");
        /// <summary>Instance is read-only.</summary>
        internal static string @InvalidOperation_ReadOnly => GetResourceString("InvalidOperation_ReadOnly");
        /// <summary>The thread was created with a ThreadStart delegate that does not accept a parameter.</summary>
        internal static string @InvalidOperation_ThreadWrongThreadStart => GetResourceString("InvalidOperation_ThreadWrongThreadStart");
        /// <summary>Unknown enum type.</summary>
        internal static string @InvalidOperation_UnknownEnumType => GetResourceString("InvalidOperation_UnknownEnumType");
        /// <summary>This property has already been set and cannot be modified.</summary>
        internal static string @InvalidOperation_WriteOnce => GetResourceString("InvalidOperation_WriteOnce");
        /// <summary>Array.CreateInstance() can only accept Type objects created by the runtime.</summary>
        internal static string @InvalidOperation_ArrayCreateInstance_NotARuntimeType => GetResourceString("InvalidOperation_ArrayCreateInstance_NotARuntimeType");
        /// <summary>Internal Error: This operation cannot be invoked in an eager class constructor.</summary>
        internal static string @InvalidOperation_TooEarly => GetResourceString("InvalidOperation_TooEarly");
        /// <summary>Cannot call Set on a null context</summary>
        internal static string @InvalidOperation_NullContext => GetResourceString("InvalidOperation_NullContext");
        /// <summary>AsyncFlowControl object must be used on the thread where it was created.</summary>
        internal static string @InvalidOperation_CannotUseAFCOtherThread => GetResourceString("InvalidOperation_CannotUseAFCOtherThread");
        /// <summary>Cannot restore context flow when it is not suppressed.</summary>
        internal static string @InvalidOperation_CannotRestoreUnsupressedFlow => GetResourceString("InvalidOperation_CannotRestoreUnsupressedFlow");
        /// <summary>Context flow is already suppressed.</summary>
        internal static string @InvalidOperation_CannotSupressFlowMultipleTimes => GetResourceString("InvalidOperation_CannotSupressFlowMultipleTimes");
        /// <summary>AsyncFlowControl object can be used only once to call Undo().</summary>
        internal static string @InvalidOperation_CannotUseAFCMultiple => GetResourceString("InvalidOperation_CannotUseAFCMultiple");
        /// <summary>AsyncFlowControl objects can be used to restore flow only on a Context that had its flow suppressed.</summary>
        internal static string @InvalidOperation_AsyncFlowCtrlCtxMismatch => GetResourceString("InvalidOperation_AsyncFlowCtrlCtxMismatch");
        /// <summary>The stream is currently in use by a previous operation on the stream.</summary>
        internal static string @InvalidOperation_AsyncIOInProgress => GetResourceString("InvalidOperation_AsyncIOInProgress");
        /// <summary>Common Language Runtime detected an invalid program.</summary>
        internal static string @InvalidProgram_Default => GetResourceString("InvalidProgram_Default");
        /// <summary>Common Language Runtime detected an invalid program. The body of method '{0}' is invalid.</summary>
        internal static string @InvalidProgram_Specific => GetResourceString("InvalidProgram_Specific");
        /// <summary>Method '{0}' has a variable argument list. Variable argument lists are not supported in .NET Core.</summary>
        internal static string @InvalidProgram_Vararg => GetResourceString("InvalidProgram_Vararg");
        /// <summary>Object.Finalize() can not be called directly. It is only callable by the runtime.</summary>
        internal static string @InvalidProgram_CallVirtFinalize => GetResourceString("InvalidProgram_CallVirtFinalize");
        /// <summary>UnmanagedCallersOnly method cannot be called from managed code.</summary>
        internal static string @InvalidProgram_UnmanagedCallersOnly => GetResourceString("InvalidProgram_UnmanagedCallersOnly");
        /// <summary>The time zone ID '{0}' was found on the local computer, but the registry information was corrupt.</summary>
        internal static string @InvalidTimeZone_InvalidRegistryData => GetResourceString("InvalidTimeZone_InvalidRegistryData");
        /// <summary>The time zone ID '{0}' was found on the local computer, but the file at '{1}' was corrupt.</summary>
        internal static string @InvalidTimeZone_InvalidFileData => GetResourceString("InvalidTimeZone_InvalidFileData");
        /// <summary>Invalid Julian day in POSIX strings.</summary>
        internal static string @InvalidTimeZone_InvalidJulianDay => GetResourceString("InvalidTimeZone_InvalidJulianDay");
        /// <summary>Julian n day in POSIX strings is not supported.</summary>
        internal static string @InvalidTimeZone_NJulianDayNotSupported => GetResourceString("InvalidTimeZone_NJulianDayNotSupported");
        /// <summary>There are no ttinfo structures in the tzfile.  At least one ttinfo structure is required in order to construct a TimeZoneInfo object.</summary>
        internal static string @InvalidTimeZone_NoTTInfoStructures => GetResourceString("InvalidTimeZone_NoTTInfoStructures");
        /// <summary>'{0}' is not a valid POSIX-TZ-environment-variable MDate rule.  A valid rule has the format 'Mm.w.d'.</summary>
        internal static string @InvalidTimeZone_UnparseablePosixMDateString => GetResourceString("InvalidTimeZone_UnparseablePosixMDateString");
        /// <summary>Could not find the drive '{0}'. The drive might not be ready or might not be mapped.</summary>
        internal static string @IO_DriveNotFound_Drive => GetResourceString("IO_DriveNotFound_Drive");
        /// <summary>The file '{0}' already exists.</summary>
        internal static string @IO_FileExists_Name => GetResourceString("IO_FileExists_Name");
        /// <summary>File name: '{0}'</summary>
        internal static string @IO_FileName_Name => GetResourceString("IO_FileName_Name");
        /// <summary>Unable to find the specified file.</summary>
        internal static string @IO_FileNotFound => GetResourceString("IO_FileNotFound");
        /// <summary>Could not load file or assembly '{0}'. The system cannot find the file specified.</summary>
        internal static string @IO_FileNotFound_FileName => GetResourceString("IO_FileNotFound_FileName");
        /// <summary>Could not load the specified file.</summary>
        internal static string @IO_FileLoad => GetResourceString("IO_FileLoad");
        /// <summary>Could not load the file '{0}'.</summary>
        internal static string @IO_FileLoad_FileName => GetResourceString("IO_FileLoad_FileName");
        /// <summary>Could not find a part of the path.</summary>
        internal static string @IO_PathNotFound_NoPathName => GetResourceString("IO_PathNotFound_NoPathName");
        /// <summary>Could not find a part of the path '{0}'.</summary>
        internal static string @IO_PathNotFound_Path => GetResourceString("IO_PathNotFound_Path");
        /// <summary>The specified file name or path is too long, or a component of the specified path is too long.</summary>
        internal static string @IO_PathTooLong => GetResourceString("IO_PathTooLong");
        /// <summary>The path '{0}' is too long, or a component of the specified path is too long.</summary>
        internal static string @IO_PathTooLong_Path => GetResourceString("IO_PathTooLong_Path");
        /// <summary>The process cannot access the file '{0}' because it is being used by another process.</summary>
        internal static string @IO_SharingViolation_File => GetResourceString("IO_SharingViolation_File");
        /// <summary>The process cannot access the file because it is being used by another process.</summary>
        internal static string @IO_SharingViolation_NoFileName => GetResourceString("IO_SharingViolation_NoFileName");
        /// <summary>Cannot create '{0}' because a file or directory with the same name already exists.</summary>
        internal static string @IO_AlreadyExists_Name => GetResourceString("IO_AlreadyExists_Name");
        /// <summary>Failed to create '{0}' with allocation size '{1}' because the disk was full.</summary>
        internal static string @IO_DiskFull_Path_AllocationSize => GetResourceString("IO_DiskFull_Path_AllocationSize");
        /// <summary>Failed to create '{0}' with allocation size '{1}' because the file was too large.</summary>
        internal static string @IO_FileTooLarge_Path_AllocationSize => GetResourceString("IO_FileTooLarge_Path_AllocationSize");
        /// <summary>Access to the path is denied.</summary>
        internal static string @UnauthorizedAccess_IODenied_NoPathName => GetResourceString("UnauthorizedAccess_IODenied_NoPathName");
        /// <summary>Access to the path '{0}' is denied.</summary>
        internal static string @UnauthorizedAccess_IODenied_Path => GetResourceString("UnauthorizedAccess_IODenied_Path");
        /// <summary>The lazily-initialized type does not have a public, parameterless constructor.</summary>
        internal static string @Lazy_CreateValue_NoParameterlessCtorForT => GetResourceString("Lazy_CreateValue_NoParameterlessCtorForT");
        /// <summary>The mode argument specifies an invalid value.</summary>
        internal static string @Lazy_ctor_ModeInvalid => GetResourceString("Lazy_ctor_ModeInvalid");
        /// <summary>ValueFactory returned null.</summary>
        internal static string @Lazy_StaticInit_InvalidOperation => GetResourceString("Lazy_StaticInit_InvalidOperation");
        /// <summary>Value is not created.</summary>
        internal static string @Lazy_ToString_ValueNotCreated => GetResourceString("Lazy_ToString_ValueNotCreated");
        /// <summary>ValueFactory attempted to access the Value property of this instance.</summary>
        internal static string @Lazy_Value_RecursiveCallsToValue => GetResourceString("Lazy_Value_RecursiveCallsToValue");
        /// <summary>Constructor on type '{0}' not found.</summary>
        internal static string @MissingConstructor_Name => GetResourceString("MissingConstructor_Name");
        /// <summary>An assembly (probably '{1}') must be rewritten using the code contracts binary rewriter (CCRewrite) because it is calling Contract.{0} and the CONTRACTS_FULL symbol is defined.  Remove any explicit definitions of the CONTRACTS_FULL symbol from your project ...</summary>
        internal static string @MustUseCCRewrite => GetResourceString("MustUseCCRewrite");
        /// <summary>Collection was of a fixed size.</summary>
        internal static string @NotSupported_FixedSizeCollection => GetResourceString("NotSupported_FixedSizeCollection");
        /// <summary>The number of WaitHandles must be less than or equal to 64.</summary>
        internal static string @NotSupported_MaxWaitHandles => GetResourceString("NotSupported_MaxWaitHandles");
        /// <summary>The number of WaitHandles on a STA thread must be less than or equal to 63.</summary>
        internal static string @NotSupported_MaxWaitHandles_STA => GetResourceString("NotSupported_MaxWaitHandles_STA");
        /// <summary>No data is available for encoding {0}. For information on defining a custom encoding, see the documentation for the Encoding.RegisterProvider method.</summary>
        internal static string @NotSupported_NoCodepageData => GetResourceString("NotSupported_NoCodepageData");
        /// <summary>Collection is read-only.</summary>
        internal static string @NotSupported_ReadOnlyCollection => GetResourceString("NotSupported_ReadOnlyCollection");
        /// <summary>The specified operation is not supported on Ranges.</summary>
        internal static string @NotSupported_RangeCollection => GetResourceString("NotSupported_RangeCollection");
        /// <summary>The string comparison type passed in is currently not supported.</summary>
        internal static string @NotSupported_StringComparison => GetResourceString("NotSupported_StringComparison");
        /// <summary>Arrays of System.Void are not supported.</summary>
        internal static string @NotSupported_VoidArray => GetResourceString("NotSupported_VoidArray");
        /// <summary>Cannot create boxed ByRef-like values.</summary>
        internal static string @NotSupported_ByRefLike => GetResourceString("NotSupported_ByRefLike");
        /// <summary>Type is not supported.</summary>
        internal static string @NotSupported_Type => GetResourceString("NotSupported_Type");
        /// <summary>WaitAll for multiple handles on a STA thread is not supported.</summary>
        internal static string @NotSupported_WaitAllSTAThread => GetResourceString("NotSupported_WaitAllSTAThread");
        /// <summary>Overlapped I/O is not supported.</summary>
        internal static string @NotSupported_Overlapped => GetResourceString("NotSupported_Overlapped");
        /// <summary>Cannot access a disposed object.</summary>
        internal static string @ObjectDisposed_Generic => GetResourceString("ObjectDisposed_Generic");
        /// <summary>Object name: '{0}'.</summary>
        internal static string @ObjectDisposed_ObjectName_Name => GetResourceString("ObjectDisposed_ObjectName_Name");
        /// <summary>Safe handle has been closed.</summary>
        internal static string @ObjectDisposed_SafeHandleClosed => GetResourceString("ObjectDisposed_SafeHandleClosed");
        /// <summary>Value was either too large or too small for an unsigned byte.</summary>
        internal static string @Overflow_Byte => GetResourceString("Overflow_Byte");
        /// <summary>Value was either too large or too small for a character.</summary>
        internal static string @Overflow_Char => GetResourceString("Overflow_Char");
        /// <summary>Value was either too large or too small for a Decimal.</summary>
        internal static string @Overflow_Decimal => GetResourceString("Overflow_Decimal");
        /// <summary>Value was either too large or too small for a Double.</summary>
        internal static string @Overflow_Double => GetResourceString("Overflow_Double");
        /// <summary>The TimeSpan could not be parsed because at least one of the numeric components is out of range or contains too many digits.</summary>
        internal static string @Overflow_TimeSpanElementTooLarge => GetResourceString("Overflow_TimeSpanElementTooLarge");
        /// <summary>The duration cannot be returned for TimeSpan.MinValue because the absolute value of TimeSpan.MinValue exceeds the value of TimeSpan.MaxValue.</summary>
        internal static string @Overflow_Duration => GetResourceString("Overflow_Duration");
        /// <summary>Value was either too large or too small for an Int16.</summary>
        internal static string @Overflow_Int16 => GetResourceString("Overflow_Int16");
        /// <summary>Value was either too large or too small for an Int32.</summary>
        internal static string @Overflow_Int32 => GetResourceString("Overflow_Int32");
        /// <summary>Value was either too large or too small for an Int64.</summary>
        internal static string @Overflow_Int64 => GetResourceString("Overflow_Int64");
        /// <summary>Negating the minimum value of a twos complement number is invalid.</summary>
        internal static string @Overflow_NegateTwosCompNum => GetResourceString("Overflow_NegateTwosCompNum");
        /// <summary>The string was being parsed as an unsigned number and could not have a negative sign.</summary>
        internal static string @Overflow_NegativeUnsigned => GetResourceString("Overflow_NegativeUnsigned");
        /// <summary>Value was either too large or too small for a signed byte.</summary>
        internal static string @Overflow_SByte => GetResourceString("Overflow_SByte");
        /// <summary>Value was either too large or too small for a Single.</summary>
        internal static string @Overflow_Single => GetResourceString("Overflow_Single");
        /// <summary>TimeSpan overflowed because the duration is too long.</summary>
        internal static string @Overflow_TimeSpanTooLong => GetResourceString("Overflow_TimeSpanTooLong");
        /// <summary>Value was either too large or too small for a UInt16.</summary>
        internal static string @Overflow_UInt16 => GetResourceString("Overflow_UInt16");
        /// <summary>Value was either too large or too small for a UInt32.</summary>
        internal static string @Overflow_UInt32 => GetResourceString("Overflow_UInt32");
        /// <summary>Value was either too large or too small for a UInt64.</summary>
        internal static string @Overflow_UInt64 => GetResourceString("Overflow_UInt64");
        /// <summary>Only single dimension arrays are supported here.</summary>
        internal static string @Rank_MultiDimNotSupported => GetResourceString("Rank_MultiDimNotSupported");
        /// <summary>An object that does not derive from System.Exception has been wrapped in a RuntimeWrappedException.</summary>
        internal static string @RuntimeWrappedException => GetResourceString("RuntimeWrappedException");
        /// <summary>The condition argument is null.</summary>
        internal static string @SpinWait_SpinUntil_ArgumentNull => GetResourceString("SpinWait_SpinUntil_ArgumentNull");
        /// <summary>The value of the field '{0}' is invalid.  The serialized data is corrupt.</summary>
        internal static string @Serialization_CorruptField => GetResourceString("Serialization_CorruptField");
        /// <summary>An error occurred while deserializing the object.  The serialized data is corrupt.</summary>
        internal static string @Serialization_InvalidData => GetResourceString("Serialization_InvalidData");
        /// <summary>The serialized data contained an invalid escape sequence '\\{0}'.</summary>
        internal static string @Serialization_InvalidEscapeSequence => GetResourceString("Serialization_InvalidEscapeSequence");
        /// <summary>Only system-provided types can be passed to the GetUninitializedObject method. '{0}' is not a valid instance of a type.</summary>
        internal static string @Serialization_InvalidType => GetResourceString("Serialization_InvalidType");
        /// <summary>The timeout must represent a value between -1 and Int32.MaxValue, inclusive.</summary>
        internal static string @SpinWait_SpinUntil_TimeoutWrong => GetResourceString("SpinWait_SpinUntil_TimeoutWrong");
        /// <summary>The wait completed due to an abandoned mutex.</summary>
        internal static string @Threading_AbandonedMutexException => GetResourceString("Threading_AbandonedMutexException");
        /// <summary>Adding the specified count to the semaphore would cause it to exceed its maximum count.</summary>
        internal static string @Threading_SemaphoreFullException => GetResourceString("Threading_SemaphoreFullException");
        /// <summary>Thread was interrupted from a waiting state.</summary>
        internal static string @Threading_ThreadInterrupted => GetResourceString("Threading_ThreadInterrupted");
        /// <summary>No handle of the given name exists.</summary>
        internal static string @Threading_WaitHandleCannotBeOpenedException => GetResourceString("Threading_WaitHandleCannotBeOpenedException");
        /// <summary>A WaitHandle with system-wide name '{0}' cannot be created. A WaitHandle of a different type might have the same name.</summary>
        internal static string @Threading_WaitHandleCannotBeOpenedException_InvalidHandle => GetResourceString("Threading_WaitHandleCannotBeOpenedException_InvalidHandle");
        /// <summary>The WaitHandle cannot be signaled because it would exceed its maximum count.</summary>
        internal static string @Threading_WaitHandleTooManyPosts => GetResourceString("Threading_WaitHandleTooManyPosts");
        /// <summary>The time zone ID '{0}' was not found on the local computer.</summary>
        internal static string @TimeZoneNotFound_MissingData => GetResourceString("TimeZoneNotFound_MissingData");
        /// <summary>Type constructor threw an exception.</summary>
        internal static string @TypeInitialization_Default => GetResourceString("TypeInitialization_Default");
        /// <summary>The type initializer for '{0}' threw an exception.</summary>
        internal static string @TypeInitialization_Type => GetResourceString("TypeInitialization_Type");
        /// <summary>A type initializer threw an exception. To determine which type, inspect the InnerException's StackTrace property.</summary>
        internal static string @TypeInitialization_Type_NoTypeAvailable => GetResourceString("TypeInitialization_Type_NoTypeAvailable");
        /// <summary>Operation could destabilize the runtime.</summary>
        internal static string @Verification_Exception => GetResourceString("Verification_Exception");
        /// <summary>Enum underlying type and the object must be same type or object. Type passed in was '{0}'; the enum underlying type was '{1}'.</summary>
        internal static string @Arg_EnumFormatUnderlyingTypeAndObjectMustBeSameType => GetResourceString("Arg_EnumFormatUnderlyingTypeAndObjectMustBeSameType");
        /// <summary>Format String can be only 'G', 'g', 'X', 'x', 'F', 'f', 'D' or 'd'.</summary>
        internal static string @Format_InvalidEnumFormatSpecification => GetResourceString("Format_InvalidEnumFormatSpecification");
        /// <summary>The value passed in must be an enum base or an underlying type for an enum, such as an Int32.</summary>
        internal static string @Arg_MustBeEnumBaseTypeOrEnum => GetResourceString("Arg_MustBeEnumBaseTypeOrEnum");
        /// <summary>Enum underlying type and the object must be same type or object must be a String. Type passed in was '{0}'; the enum underlying type was '{1}'.</summary>
        internal static string @Arg_EnumUnderlyingTypeAndObjectMustBeSameType => GetResourceString("Arg_EnumUnderlyingTypeAndObjectMustBeSameType");
        /// <summary>Type must be a type provided by the runtime.</summary>
        internal static string @Arg_MustBeType => GetResourceString("Arg_MustBeType");
        /// <summary>Must specify valid information for parsing in the string.</summary>
        internal static string @Arg_MustContainEnumInfo => GetResourceString("Arg_MustContainEnumInfo");
        /// <summary>Requested value '{0}' was not found.</summary>
        internal static string @Arg_EnumValueNotFound => GetResourceString("Arg_EnumValueNotFound");
        /// <summary>Destination array was not long enough. Check the destination index, length, and the array's lower bounds.</summary>
        internal static string @Arg_LongerThanDestArray => GetResourceString("Arg_LongerThanDestArray");
        /// <summary>Source array was not long enough. Check the source index, length, and the array's lower bounds.</summary>
        internal static string @Arg_LongerThanSrcArray => GetResourceString("Arg_LongerThanSrcArray");
        /// <summary>String cannot be of zero length.</summary>
        internal static string @Argument_StringZeroLength => GetResourceString("Argument_StringZeroLength");
        /// <summary>The first char in the string is the null character.</summary>
        internal static string @Argument_StringFirstCharIsZero => GetResourceString("Argument_StringFirstCharIsZero");
        /// <summary>Environment variable name or value is too long.</summary>
        internal static string @Argument_LongEnvVarValue => GetResourceString("Argument_LongEnvVarValue");
        /// <summary>Environment variable name cannot contain equal character.</summary>
        internal static string @Argument_IllegalEnvVarName => GetResourceString("Argument_IllegalEnvVarName");
        /// <summary>Assumption failed.</summary>
        internal static string @AssumptionFailed => GetResourceString("AssumptionFailed");
        /// <summary>Assumption failed: {0}</summary>
        internal static string @AssumptionFailed_Cnd => GetResourceString("AssumptionFailed_Cnd");
        /// <summary>Assertion failed.</summary>
        internal static string @AssertionFailed => GetResourceString("AssertionFailed");
        /// <summary>Assertion failed: {0}</summary>
        internal static string @AssertionFailed_Cnd => GetResourceString("AssertionFailed_Cnd");
        /// <summary>Precondition failed.</summary>
        internal static string @PreconditionFailed => GetResourceString("PreconditionFailed");
        /// <summary>Precondition failed: {0}</summary>
        internal static string @PreconditionFailed_Cnd => GetResourceString("PreconditionFailed_Cnd");
        /// <summary>Postcondition failed.</summary>
        internal static string @PostconditionFailed => GetResourceString("PostconditionFailed");
        /// <summary>Postcondition failed: {0}</summary>
        internal static string @PostconditionFailed_Cnd => GetResourceString("PostconditionFailed_Cnd");
        /// <summary>Postcondition failed after throwing an exception.</summary>
        internal static string @PostconditionOnExceptionFailed => GetResourceString("PostconditionOnExceptionFailed");
        /// <summary>Postcondition failed after throwing an exception: {0}</summary>
        internal static string @PostconditionOnExceptionFailed_Cnd => GetResourceString("PostconditionOnExceptionFailed_Cnd");
        /// <summary>The home directory of the current user could not be determined.</summary>
        internal static string @PersistedFiles_NoHomeDirectory => GetResourceString("PersistedFiles_NoHomeDirectory");
        /// <summary>Invariant failed.</summary>
        internal static string @InvariantFailed => GetResourceString("InvariantFailed");
        /// <summary>Invariant failed: {0}</summary>
        internal static string @InvariantFailed_Cnd => GetResourceString("InvariantFailed_Cnd");
        /// <summary>Could not find a resource entry for the encoding codepage '{0} - {1}'</summary>
        internal static string @MissingEncodingNameResource => GetResourceString("MissingEncodingNameResource");
        /// <summary>Unicode</summary>
        internal static string @Globalization_cp_1200 => GetResourceString("Globalization_cp_1200");
        /// <summary>Unicode (Big-Endian)</summary>
        internal static string @Globalization_cp_1201 => GetResourceString("Globalization_cp_1201");
        /// <summary>Unicode (UTF-32)</summary>
        internal static string @Globalization_cp_12000 => GetResourceString("Globalization_cp_12000");
        /// <summary>Unicode (UTF-32 Big-Endian)</summary>
        internal static string @Globalization_cp_12001 => GetResourceString("Globalization_cp_12001");
        /// <summary>US-ASCII</summary>
        internal static string @Globalization_cp_20127 => GetResourceString("Globalization_cp_20127");
        /// <summary>Western European (ISO)</summary>
        internal static string @Globalization_cp_28591 => GetResourceString("Globalization_cp_28591");
        /// <summary>Unicode (UTF-7)</summary>
        internal static string @Globalization_cp_65000 => GetResourceString("Globalization_cp_65000");
        /// <summary>Unicode (UTF-8)</summary>
        internal static string @Globalization_cp_65001 => GetResourceString("Globalization_cp_65001");
        /// <summary>---- DEBUG ASSERTION FAILED ----</summary>
        internal static string @DebugAssertBanner => GetResourceString("DebugAssertBanner");
        /// <summary>---- Assert Long Message ----</summary>
        internal static string @DebugAssertLongMessage => GetResourceString("DebugAssertLongMessage");
        /// <summary>---- Assert Short Message ----</summary>
        internal static string @DebugAssertShortMessage => GetResourceString("DebugAssertShortMessage");
        /// <summary>Object cannot be cast to Empty.</summary>
        internal static string @InvalidCast_Empty => GetResourceString("InvalidCast_Empty");
        /// <summary>Unknown TypeCode value.</summary>
        internal static string @Arg_UnknownTypeCode => GetResourceString("Arg_UnknownTypeCode");
        /// <summary>Could not determine the order of year, month, and date from '{0}'.</summary>
        internal static string @Format_BadDatePattern => GetResourceString("Format_BadDatePattern");
        /// <summary>String '{0}' was not recognized as a valid DateTime.</summary>
        internal static string @Format_BadDateTime => GetResourceString("Format_BadDateTime");
        /// <summary>String '{0}' was not recognized as a valid DateOnly.</summary>
        internal static string @Format_BadDateOnly => GetResourceString("Format_BadDateOnly");
        /// <summary>String '{0}' was not recognized as a valid TimeOnly.</summary>
        internal static string @Format_BadTimeOnly => GetResourceString("Format_BadTimeOnly");
        /// <summary>String '{0}' contains parts which are not specific to the {1}.</summary>
        internal static string @Format_DateTimeOnlyContainsNoneDateParts => GetResourceString("Format_DateTimeOnlyContainsNoneDateParts");
        /// <summary>The DateTime represented by the string '{0}' is not supported in calendar '{1}'.</summary>
        internal static string @Format_BadDateTimeCalendar => GetResourceString("Format_BadDateTimeCalendar");
        /// <summary>String '{0}' was not recognized as a valid DateTime because the day of week was incorrect.</summary>
        internal static string @Format_BadDayOfWeek => GetResourceString("Format_BadDayOfWeek");
        /// <summary>The DateTime represented by the string '{0}' is out of range.</summary>
        internal static string @Format_DateOutOfRange => GetResourceString("Format_DateOutOfRange");
        /// <summary>There must be at least a partial date with a year present in the input string '{0}'.</summary>
        internal static string @Format_MissingIncompleteDate => GetResourceString("Format_MissingIncompleteDate");
        /// <summary>The time zone offset of string '{0}' must be within plus or minus 14 hours.</summary>
        internal static string @Format_OffsetOutOfRange => GetResourceString("Format_OffsetOutOfRange");
        /// <summary>DateTime pattern '{0}' appears more than once with different values.</summary>
        internal static string @Format_RepeatDateTimePattern => GetResourceString("Format_RepeatDateTimePattern");
        /// <summary>The string '{0}' was not recognized as a valid DateTime. There is an unknown word starting at index '{1}'.</summary>
        internal static string @Format_UnknownDateTimeWord => GetResourceString("Format_UnknownDateTimeWord");
        /// <summary>The UTC representation of the date '{0}' falls outside the year range 1-9999.</summary>
        internal static string @Format_UTCOutOfRange => GetResourceString("Format_UTCOutOfRange");
        /// <summary>Ambiguous match found.</summary>
        internal static string @RFLCT_Ambiguous => GetResourceString("RFLCT_Ambiguous");
        /// <summary>One or more errors occurred.</summary>
        internal static string @AggregateException_ctor_DefaultMessage => GetResourceString("AggregateException_ctor_DefaultMessage");
        /// <summary>An element of innerExceptions was null.</summary>
        internal static string @AggregateException_ctor_InnerExceptionNull => GetResourceString("AggregateException_ctor_InnerExceptionNull");
        /// <summary>The serialization stream contains no inner exceptions.</summary>
        internal static string @AggregateException_DeserializationFailure => GetResourceString("AggregateException_DeserializationFailure");
        /// <summary>(Inner Exception #{0})</summary>
        internal static string @AggregateException_InnerException => GetResourceString("AggregateException_InnerException");
        /// <summary>Time-out interval must be less than 2^32-2.</summary>
        internal static string @ArgumentOutOfRange_TimeoutTooLarge => GetResourceString("ArgumentOutOfRange_TimeoutTooLarge");
        /// <summary>Period must be less than 2^32-2.</summary>
        internal static string @ArgumentOutOfRange_PeriodTooLarge => GetResourceString("ArgumentOutOfRange_PeriodTooLarge");
        /// <summary>The current SynchronizationContext may not be used as a TaskScheduler.</summary>
        internal static string @TaskScheduler_FromCurrentSynchronizationContext_NoCurrent => GetResourceString("TaskScheduler_FromCurrentSynchronizationContext_NoCurrent");
        /// <summary>ExecuteTask may not be called for a task which was previously queued to a different TaskScheduler.</summary>
        internal static string @TaskScheduler_ExecuteTask_WrongTaskScheduler => GetResourceString("TaskScheduler_ExecuteTask_WrongTaskScheduler");
        /// <summary>The TryExecuteTaskInline call to the underlying scheduler succeeded, but the task body was not invoked.</summary>
        internal static string @TaskScheduler_InconsistentStateAfterTryExecuteTaskInline => GetResourceString("TaskScheduler_InconsistentStateAfterTryExecuteTaskInline");
        /// <summary>An exception was thrown by a TaskScheduler.</summary>
        internal static string @TaskSchedulerException_ctor_DefaultMessage => GetResourceString("TaskSchedulerException_ctor_DefaultMessage");
        /// <summary>It is invalid to exclude specific continuation kinds for continuations off of multiple tasks.</summary>
        internal static string @Task_MultiTaskContinuation_FireOptions => GetResourceString("Task_MultiTaskContinuation_FireOptions");
        /// <summary>The specified TaskContinuationOptions combined LongRunning and ExecuteSynchronously.  Synchronous continuations should not be long running.</summary>
        internal static string @Task_ContinueWith_ESandLR => GetResourceString("Task_ContinueWith_ESandLR");
        /// <summary>The tasks argument contains no tasks.</summary>
        internal static string @Task_MultiTaskContinuation_EmptyTaskList => GetResourceString("Task_MultiTaskContinuation_EmptyTaskList");
        /// <summary>The tasks argument included a null value.</summary>
        internal static string @Task_MultiTaskContinuation_NullTask => GetResourceString("Task_MultiTaskContinuation_NullTask");
        /// <summary>It is invalid to specify TaskCreationOptions.PreferFairness in calls to FromAsync.</summary>
        internal static string @Task_FromAsync_PreferFairness => GetResourceString("Task_FromAsync_PreferFairness");
        /// <summary>It is invalid to specify TaskCreationOptions.LongRunning in calls to FromAsync.</summary>
        internal static string @Task_FromAsync_LongRunning => GetResourceString("Task_FromAsync_LongRunning");
        /// <summary>The builder was not properly initialized.</summary>
        internal static string @AsyncMethodBuilder_InstanceNotInitialized => GetResourceString("AsyncMethodBuilder_InstanceNotInitialized");
        /// <summary>An attempt was made to transition a task to a final state when it had already completed.</summary>
        internal static string @TaskT_TransitionToFinal_AlreadyCompleted => GetResourceString("TaskT_TransitionToFinal_AlreadyCompleted");
        /// <summary>{Not yet computed}</summary>
        internal static string @TaskT_DebuggerNoResult => GetResourceString("TaskT_DebuggerNoResult");
        /// <summary>The operation was canceled.</summary>
        internal static string @OperationCanceled => GetResourceString("OperationCanceled");
        /// <summary>No tokens were supplied.</summary>
        internal static string @CancellationToken_CreateLinkedToken_TokensIsEmpty => GetResourceString("CancellationToken_CreateLinkedToken_TokensIsEmpty");
        /// <summary>The CancellationTokenSource has been disposed.</summary>
        internal static string @CancellationTokenSource_Disposed => GetResourceString("CancellationTokenSource_Disposed");
        /// <summary>The CancellationTokenSource associated with this CancellationToken has been disposed.</summary>
        internal static string @CancellationToken_SourceDisposed => GetResourceString("CancellationToken_SourceDisposed");
        /// <summary>(Internal)Expected an Exception or an IEnumerable&lt;Exception&gt;</summary>
        internal static string @TaskExceptionHolder_UnknownExceptionType => GetResourceString("TaskExceptionHolder_UnknownExceptionType");
        /// <summary>A Task's exception(s) were not observed either by Waiting on the Task or accessing its Exception property. As a result, the unobserved exception was rethrown by the finalizer thread.</summary>
        internal static string @TaskExceptionHolder_UnhandledException => GetResourceString("TaskExceptionHolder_UnhandledException");
        /// <summary>The value needs to be either -1 (signifying an infinite timeout), 0 or a positive integer.</summary>
        internal static string @Task_Delay_InvalidMillisecondsDelay => GetResourceString("Task_Delay_InvalidMillisecondsDelay");
        /// <summary>The value needs to translate in milliseconds to -1 (signifying an infinite timeout), 0 or a positive integer less than or equal to Int32.MaxValue.</summary>
        internal static string @Task_Delay_InvalidDelay => GetResourceString("Task_Delay_InvalidDelay");
        /// <summary>A task may only be disposed if it is in a completion state (RanToCompletion, Faulted or Canceled).</summary>
        internal static string @Task_Dispose_NotCompleted => GetResourceString("Task_Dispose_NotCompleted");
        /// <summary>The tasks array included at least one null element.</summary>
        internal static string @Task_WaitMulti_NullTask => GetResourceString("Task_WaitMulti_NullTask");
        /// <summary>The specified TaskContinuationOptions excluded all continuation kinds.</summary>
        internal static string @Task_ContinueWith_NotOnAnything => GetResourceString("Task_ContinueWith_NotOnAnything");
        /// <summary>The value needs to translate in milliseconds to -1 (signifying an infinite timeout), 0, or a positive integer less than or equal to the maximum allowed timer duration.</summary>
        internal static string @Task_InvalidTimerTimeSpan => GetResourceString("Task_InvalidTimerTimeSpan");
        /// <summary>RunSynchronously may not be called on a task that was already started.</summary>
        internal static string @Task_RunSynchronously_AlreadyStarted => GetResourceString("Task_RunSynchronously_AlreadyStarted");
        /// <summary>The task has been disposed.</summary>
        internal static string @Task_ThrowIfDisposed => GetResourceString("Task_ThrowIfDisposed");
        /// <summary>RunSynchronously may not be called on a task that has already completed.</summary>
        internal static string @Task_RunSynchronously_TaskCompleted => GetResourceString("Task_RunSynchronously_TaskCompleted");
        /// <summary>RunSynchronously may not be called on a task not bound to a delegate, such as the task returned from an asynchronous method.</summary>
        internal static string @Task_RunSynchronously_Promise => GetResourceString("Task_RunSynchronously_Promise");
        /// <summary>RunSynchronously may not be called on a continuation task.</summary>
        internal static string @Task_RunSynchronously_Continuation => GetResourceString("Task_RunSynchronously_Continuation");
        /// <summary>Start may not be called on a task that was already started.</summary>
        internal static string @Task_Start_AlreadyStarted => GetResourceString("Task_Start_AlreadyStarted");
        /// <summary>Start may not be called on a continuation task.</summary>
        internal static string @Task_Start_ContinuationTask => GetResourceString("Task_Start_ContinuationTask");
        /// <summary>Start may not be called on a promise-style task.</summary>
        internal static string @Task_Start_Promise => GetResourceString("Task_Start_Promise");
        /// <summary>Start may not be called on a task that has completed.</summary>
        internal static string @Task_Start_TaskCompleted => GetResourceString("Task_Start_TaskCompleted");
        /// <summary>A task was canceled.</summary>
        internal static string @TaskCanceledException_ctor_DefaultMessage => GetResourceString("TaskCanceledException_ctor_DefaultMessage");
        /// <summary>The exceptions collection was empty.</summary>
        internal static string @TaskCompletionSourceT_TrySetException_NoExceptions => GetResourceString("TaskCompletionSourceT_TrySetException_NoExceptions");
        /// <summary>The exceptions collection included at least one null element.</summary>
        internal static string @TaskCompletionSourceT_TrySetException_NullException => GetResourceString("TaskCompletionSourceT_TrySetException_NullException");
        /// <summary>'{0}' cannot be greater than {1}.</summary>
        internal static string @Argument_MinMaxValue => GetResourceString("Argument_MinMaxValue");
        /// <summary>An exception was not handled in an AsyncLocal&lt;T&gt; notification callback.</summary>
        internal static string @ExecutionContext_ExceptionInAsyncLocalNotification => GetResourceString("ExecutionContext_ExceptionInAsyncLocalNotification");
        /// <summary>Either the IAsyncResult object did not come from the corresponding async method on this type, or the End method was called multiple times with the same IAsyncResult.</summary>
        internal static string @InvalidOperation_WrongAsyncResultOrEndCalledMultiple => GetResourceString("InvalidOperation_WrongAsyncResultOrEndCalledMultiple");
        /// <summary>Thread tracking is disabled.</summary>
        internal static string @SpinLock_IsHeldByCurrentThread => GetResourceString("SpinLock_IsHeldByCurrentThread");
        /// <summary>The calling thread already holds the lock.</summary>
        internal static string @SpinLock_TryEnter_LockRecursionException => GetResourceString("SpinLock_TryEnter_LockRecursionException");
        /// <summary>The calling thread does not hold the lock.</summary>
        internal static string @SpinLock_Exit_SynchronizationLockException => GetResourceString("SpinLock_Exit_SynchronizationLockException");
        /// <summary>The tookLock argument must be set to false before calling this method.</summary>
        internal static string @SpinLock_TryReliableEnter_ArgumentException => GetResourceString("SpinLock_TryReliableEnter_ArgumentException");
        /// <summary>The timeout must be a value between -1 and Int32.MaxValue, inclusive.</summary>
        internal static string @SpinLock_TryEnter_ArgumentOutOfRange => GetResourceString("SpinLock_TryEnter_ArgumentOutOfRange");
        /// <summary>The event has been disposed.</summary>
        internal static string @ManualResetEventSlim_Disposed => GetResourceString("ManualResetEventSlim_Disposed");
        /// <summary>The spinCount argument must be in the range 0 to {0}, inclusive.</summary>
        internal static string @ManualResetEventSlim_ctor_SpinCountOutOfRange => GetResourceString("ManualResetEventSlim_ctor_SpinCountOutOfRange");
        /// <summary>There are too many threads currently waiting on the event. A maximum of {0} waiting threads are supported.</summary>
        internal static string @ManualResetEventSlim_ctor_TooManyWaiters => GetResourceString("ManualResetEventSlim_ctor_TooManyWaiters");
        /// <summary>Send is not supported in the Windows Runtime SynchronizationContext</summary>
        internal static string @InvalidOperation_SendNotSupportedOnWindowsRTSynchronizationContext => GetResourceString("InvalidOperation_SendNotSupportedOnWindowsRTSynchronizationContext");
        /// <summary>The semaphore has been disposed.</summary>
        internal static string @SemaphoreSlim_Disposed => GetResourceString("SemaphoreSlim_Disposed");
        /// <summary>The releaseCount argument must be greater than zero.</summary>
        internal static string @SemaphoreSlim_Release_CountWrong => GetResourceString("SemaphoreSlim_Release_CountWrong");
        /// <summary>The timeout must represent a value between -1 and Int32.MaxValue, inclusive.</summary>
        internal static string @SemaphoreSlim_Wait_TimeoutWrong => GetResourceString("SemaphoreSlim_Wait_TimeoutWrong");
        /// <summary>The maximumCount argument must be a positive number. If a maximum is not required, use the constructor without a maxCount parameter.</summary>
        internal static string @SemaphoreSlim_ctor_MaxCountWrong => GetResourceString("SemaphoreSlim_ctor_MaxCountWrong");
        /// <summary>The initialCount argument must be non-negative and less than or equal to the maximumCount.</summary>
        internal static string @SemaphoreSlim_ctor_InitialCountWrong => GetResourceString("SemaphoreSlim_ctor_InitialCountWrong");
        /// <summary>The ThreadLocal object is not tracking values. To use the Values property, use a ThreadLocal constructor that accepts the trackAllValues parameter and set the parameter to true.</summary>
        internal static string @ThreadLocal_ValuesNotAvailable => GetResourceString("ThreadLocal_ValuesNotAvailable");
        /// <summary>ValueFactory attempted to access the Value property of this instance.</summary>
        internal static string @ThreadLocal_Value_RecursiveCallsToValue => GetResourceString("ThreadLocal_Value_RecursiveCallsToValue");
        /// <summary>The ThreadLocal object has been disposed.</summary>
        internal static string @ThreadLocal_Disposed => GetResourceString("ThreadLocal_Disposed");
        /// <summary>Write lock may not be acquired with read lock held. This pattern is prone to deadlocks. Please ensure that read locks are released before taking a write lock. If an upgrade is necessary, use an upgrade lock in place of the read lock.</summary>
        internal static string @LockRecursionException_WriteAfterReadNotAllowed => GetResourceString("LockRecursionException_WriteAfterReadNotAllowed");
        /// <summary>Recursive write lock acquisitions not allowed in this mode.</summary>
        internal static string @LockRecursionException_RecursiveWriteNotAllowed => GetResourceString("LockRecursionException_RecursiveWriteNotAllowed");
        /// <summary>A read lock may not be acquired with the write lock held in this mode.</summary>
        internal static string @LockRecursionException_ReadAfterWriteNotAllowed => GetResourceString("LockRecursionException_ReadAfterWriteNotAllowed");
        /// <summary>Recursive upgradeable lock acquisitions not allowed in this mode.</summary>
        internal static string @LockRecursionException_RecursiveUpgradeNotAllowed => GetResourceString("LockRecursionException_RecursiveUpgradeNotAllowed");
        /// <summary>Recursive read lock acquisitions not allowed in this mode.</summary>
        internal static string @LockRecursionException_RecursiveReadNotAllowed => GetResourceString("LockRecursionException_RecursiveReadNotAllowed");
        /// <summary>The lock is being disposed while still being used. It either is being held by a thread and/or has active waiters waiting to acquire the lock.</summary>
        internal static string @SynchronizationLockException_IncorrectDispose => GetResourceString("SynchronizationLockException_IncorrectDispose");
        /// <summary>The write lock is being released without being held.</summary>
        internal static string @SynchronizationLockException_MisMatchedWrite => GetResourceString("SynchronizationLockException_MisMatchedWrite");
        /// <summary>Upgradeable lock may not be acquired with read lock held.</summary>
        internal static string @LockRecursionException_UpgradeAfterReadNotAllowed => GetResourceString("LockRecursionException_UpgradeAfterReadNotAllowed");
        /// <summary>Upgradeable lock may not be acquired with write lock held in this mode. Acquiring Upgradeable lock gives the ability to read along with an option to upgrade to a writer.</summary>
        internal static string @LockRecursionException_UpgradeAfterWriteNotAllowed => GetResourceString("LockRecursionException_UpgradeAfterWriteNotAllowed");
        /// <summary>The upgradeable lock is being released without being held.</summary>
        internal static string @SynchronizationLockException_MisMatchedUpgrade => GetResourceString("SynchronizationLockException_MisMatchedUpgrade");
        /// <summary>The read lock is being released without being held.</summary>
        internal static string @SynchronizationLockException_MisMatchedRead => GetResourceString("SynchronizationLockException_MisMatchedRead");
        /// <summary>Timeouts are not supported on this stream.</summary>
        internal static string @InvalidOperation_TimeoutsNotSupported => GetResourceString("InvalidOperation_TimeoutsNotSupported");
        /// <summary>The Timer was already closed using an incompatible Dispose method.</summary>
        internal static string @InvalidOperation_TimerAlreadyClosed => GetResourceString("InvalidOperation_TimerAlreadyClosed");
        /// <summary>Stream does not support reading.</summary>
        internal static string @NotSupported_UnreadableStream => GetResourceString("NotSupported_UnreadableStream");
        /// <summary>Stream does not support writing.</summary>
        internal static string @NotSupported_UnwritableStream => GetResourceString("NotSupported_UnwritableStream");
        /// <summary>Cannot access a closed Stream.</summary>
        internal static string @ObjectDisposed_StreamClosed => GetResourceString("ObjectDisposed_StreamClosed");
        /// <summary>Derived classes must provide an implementation.</summary>
        internal static string @NotSupported_SubclassOverride => GetResourceString("NotSupported_SubclassOverride");
        /// <summary>Cannot remove the event handler since no public remove method exists for the event.</summary>
        internal static string @InvalidOperation_NoPublicRemoveMethod => GetResourceString("InvalidOperation_NoPublicRemoveMethod");
        /// <summary>Cannot add the event handler since no public add method exists for the event.</summary>
        internal static string @InvalidOperation_NoPublicAddMethod => GetResourceString("InvalidOperation_NoPublicAddMethod");
        /// <summary>Serialization error.</summary>
        internal static string @SerializationException => GetResourceString("SerializationException");
        /// <summary>Member '{0}' was not found.</summary>
        internal static string @Serialization_NotFound => GetResourceString("Serialization_NotFound");
        /// <summary>Version value must be positive.</summary>
        internal static string @Serialization_OptionalFieldVersionValue => GetResourceString("Serialization_OptionalFieldVersionValue");
        /// <summary>Cannot add the same member twice to a SerializationInfo object.</summary>
        internal static string @Serialization_SameNameTwice => GetResourceString("Serialization_SameNameTwice");
        /// <summary>This non-CLS method is not implemented.</summary>
        internal static string @NotSupported_AbstractNonCLS => GetResourceString("NotSupported_AbstractNonCLS");
        /// <summary>Cannot resolve {0} to a TypeInfo object.</summary>
        internal static string @NotSupported_NoTypeInfo => GetResourceString("NotSupported_NoTypeInfo");
        /// <summary>Binary format of the specified custom attribute was invalid.</summary>
        internal static string @Arg_CustomAttributeFormatException => GetResourceString("Arg_CustomAttributeFormatException");
        /// <summary>The member must be either a field or a property.</summary>
        internal static string @Argument_InvalidMemberForNamedArgument => GetResourceString("Argument_InvalidMemberForNamedArgument");
        /// <summary>Specified filter criteria was invalid.</summary>
        internal static string @Arg_InvalidFilterCriteriaException => GetResourceString("Arg_InvalidFilterCriteriaException");
        /// <summary>Attempt has been made to use a COM object that does not have a backing class factory.</summary>
        internal static string @Arg_InvalidComObjectException => GetResourceString("Arg_InvalidComObjectException");
        /// <summary>Specified OLE variant was invalid.</summary>
        internal static string @Arg_InvalidOleVariantTypeException => GetResourceString("Arg_InvalidOleVariantTypeException");
        /// <summary>Must specify one or more parameters.</summary>
        internal static string @Arg_ParmArraySize => GetResourceString("Arg_ParmArraySize");
        /// <summary>Type must be a Pointer.</summary>
        internal static string @Arg_MustBePointer => GetResourceString("Arg_MustBePointer");
        /// <summary>Invalid handle.</summary>
        internal static string @Arg_InvalidHandle => GetResourceString("Arg_InvalidHandle");
        /// <summary>The Enum type should contain one and only one instance field.</summary>
        internal static string @Argument_InvalidEnum => GetResourceString("Argument_InvalidEnum");
        /// <summary>Type passed in must be derived from System.Attribute or System.Attribute itself.</summary>
        internal static string @Argument_MustHaveAttributeBaseClass => GetResourceString("Argument_MustHaveAttributeBaseClass");
        /// <summary>A String must be provided for the filter criteria.</summary>
        internal static string @InvalidFilterCriteriaException_CritString => GetResourceString("InvalidFilterCriteriaException_CritString");
        /// <summary>An Int32 must be provided for the filter criteria.</summary>
        internal static string @InvalidFilterCriteriaException_CritInt => GetResourceString("InvalidFilterCriteriaException_CritInt");
        /// <summary>Adding or removing event handlers dynamically is not supported on WinRT events.</summary>
        internal static string @InvalidOperation_NotSupportedOnWinRTEvent => GetResourceString("InvalidOperation_NotSupportedOnWinRTEvent");
        /// <summary>COM Interop is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_ComInterop => GetResourceString("PlatformNotSupported_ComInterop");
        /// <summary>ReflectionOnly loading is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_ReflectionOnly => GetResourceString("PlatformNotSupported_ReflectionOnly");
        /// <summary>Locking/unlocking file regions is not supported on this platform. Use FileShare on the entire file instead.</summary>
        internal static string @PlatformNotSupported_OSXFileLocking => GetResourceString("PlatformNotSupported_OSXFileLocking");
        /// <summary>This API is specific to the way in which Windows handles asynchronous I/O, and is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_OverlappedIO => GetResourceString("PlatformNotSupported_OverlappedIO");
        /// <summary>Dynamic code generation is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_ReflectionEmit => GetResourceString("PlatformNotSupported_ReflectionEmit");
        /// <summary>Secondary AppDomains are not supported on this platform.</summary>
        internal static string @PlatformNotSupported_AppDomains => GetResourceString("PlatformNotSupported_AppDomains");
        /// <summary>Code Access Security is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_CAS => GetResourceString("PlatformNotSupported_CAS");
        /// <summary>AppDomain resource monitoring is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_AppDomain_ResMon => GetResourceString("PlatformNotSupported_AppDomain_ResMon");
        /// <summary>Windows Principal functionality is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_Principal => GetResourceString("PlatformNotSupported_Principal");
        /// <summary>Thread abort is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_ThreadAbort => GetResourceString("PlatformNotSupported_ThreadAbort");
        /// <summary>Thread suspend is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_ThreadSuspend => GetResourceString("PlatformNotSupported_ThreadSuspend");
        /// <summary>Member '{0}' not found.</summary>
        internal static string @MissingMember_Name => GetResourceString("MissingMember_Name");
        /// <summary>Method '{0}' not found.</summary>
        internal static string @MissingMethod_Name => GetResourceString("MissingMethod_Name");
        /// <summary>Field '{0}' not found.</summary>
        internal static string @MissingField_Name => GetResourceString("MissingField_Name");
        /// <summary>String cannot have zero length.</summary>
        internal static string @Format_StringZeroLength => GetResourceString("Format_StringZeroLength");
        /// <summary>Failed to get marshaler for IID {0}.</summary>
        internal static string @StandardOleMarshalObjectGetMarshalerFailed => GetResourceString("StandardOleMarshalObjectGetMarshalerFailed");
        /// <summary>The time zone ID '{0}' was found on the local computer, but the application does not have permission to read the file.</summary>
        internal static string @Security_CannotReadFileData => GetResourceString("Security_CannotReadFileData");
        /// <summary>The time zone ID '{0}' was found on the local computer, but the application does not have permission to read the registry information.</summary>
        internal static string @Security_CannotReadRegistryData => GetResourceString("Security_CannotReadRegistryData");
        /// <summary>Invalid assembly public key.</summary>
        internal static string @Security_InvalidAssemblyPublicKey => GetResourceString("Security_InvalidAssemblyPublicKey");
        /// <summary>Requested registry access is not allowed.</summary>
        internal static string @Security_RegistryPermission => GetResourceString("Security_RegistryPermission");
        /// <summary>Could not load type '{0}' from assembly '{1}'.</summary>
        internal static string @ClassLoad_General => GetResourceString("ClassLoad_General");
        /// <summary>'{0}' from assembly '{1}' has too many dimensions.</summary>
        internal static string @ClassLoad_RankTooLarge => GetResourceString("ClassLoad_RankTooLarge");
        /// <summary>Could not load type '{0}' from assembly '{1}' because generic types cannot have explicit layout.</summary>
        internal static string @ClassLoad_ExplicitGeneric => GetResourceString("ClassLoad_ExplicitGeneric");
        /// <summary>Could not load type '{0}' from assembly '{1}' because the format is invalid.</summary>
        internal static string @ClassLoad_BadFormat => GetResourceString("ClassLoad_BadFormat");
        /// <summary>Array of type '{0}' from assembly '{1}' cannot be created because base value type is too large.</summary>
        internal static string @ClassLoad_ValueClassTooLarge => GetResourceString("ClassLoad_ValueClassTooLarge");
        /// <summary>Could not load type '{0}' from assembly '{1}' because it contains an object field at offset '{2}' that is incorrectly aligned or overlapped by a non-object field.</summary>
        internal static string @ClassLoad_ExplicitLayout => GetResourceString("ClassLoad_ExplicitLayout");
        /// <summary>Method not found: '{0}'.</summary>
        internal static string @EE_MissingMethod => GetResourceString("EE_MissingMethod");
        /// <summary>Field not found: '{0}'.</summary>
        internal static string @EE_MissingField => GetResourceString("EE_MissingField");
        /// <summary>Access to the registry key '{0}' is denied.</summary>
        internal static string @UnauthorizedAccess_RegistryKeyGeneric_Key => GetResourceString("UnauthorizedAccess_RegistryKeyGeneric_Key");
        /// <summary>Unknown error '{0}'.</summary>
        internal static string @UnknownError_Num => GetResourceString("UnknownError_Num");
        /// <summary>The specified Type must be a struct containing no references.</summary>
        internal static string @Argument_NeedStructWithNoRefs => GetResourceString("Argument_NeedStructWithNoRefs");
        /// <summary>Buffer cannot be null.</summary>
        internal static string @ArgumentNull_Buffer => GetResourceString("ArgumentNull_Buffer");
        /// <summary>The number of bytes cannot exceed the virtual address space on a 32 bit machine.</summary>
        internal static string @ArgumentOutOfRange_AddressSpace => GetResourceString("ArgumentOutOfRange_AddressSpace");
        /// <summary>The length of the buffer must be less than the maximum UIntPtr value for your platform.</summary>
        internal static string @ArgumentOutOfRange_UIntPtrMax => GetResourceString("ArgumentOutOfRange_UIntPtrMax");
        /// <summary>Not enough space available in the buffer.</summary>
        internal static string @Arg_BufferTooSmall => GetResourceString("Arg_BufferTooSmall");
        /// <summary>You must call Initialize on this object instance before using it.</summary>
        internal static string @InvalidOperation_MustCallInitialize => GetResourceString("InvalidOperation_MustCallInitialize");
        /// <summary>The buffer is not associated with this pool and may not be returned to it.</summary>
        internal static string @ArgumentException_BufferNotFromPool => GetResourceString("ArgumentException_BufferNotFromPool");
        /// <summary>Offset and length were greater than the size of the SafeBuffer.</summary>
        internal static string @Argument_InvalidSafeBufferOffLen => GetResourceString("Argument_InvalidSafeBufferOffLen");
        /// <summary>Invalid seek origin.</summary>
        internal static string @Argument_InvalidSeekOrigin => GetResourceString("Argument_InvalidSeekOrigin");
        /// <summary>There are not enough bytes remaining in the accessor to read at this position.</summary>
        internal static string @Argument_NotEnoughBytesToRead => GetResourceString("Argument_NotEnoughBytesToRead");
        /// <summary>There are not enough bytes remaining in the accessor to write at this position.</summary>
        internal static string @Argument_NotEnoughBytesToWrite => GetResourceString("Argument_NotEnoughBytesToWrite");
        /// <summary>Offset and capacity were greater than the size of the view.</summary>
        internal static string @Argument_OffsetAndCapacityOutOfBounds => GetResourceString("Argument_OffsetAndCapacityOutOfBounds");
        /// <summary>UnmanagedMemoryStream length must be non-negative and less than 2^63 - 1 - baseAddress.</summary>
        internal static string @ArgumentOutOfRange_UnmanagedMemStreamLength => GetResourceString("ArgumentOutOfRange_UnmanagedMemStreamLength");
        /// <summary>The UnmanagedMemoryAccessor capacity and offset would wrap around the high end of the address space.</summary>
        internal static string @Argument_UnmanagedMemAccessorWrapAround => GetResourceString("Argument_UnmanagedMemAccessorWrapAround");
        /// <summary>Stream length must be non-negative and less than 2^31 - 1 - origin.</summary>
        internal static string @ArgumentOutOfRange_StreamLength => GetResourceString("ArgumentOutOfRange_StreamLength");
        /// <summary>The UnmanagedMemoryStream capacity would wrap around the high end of the address space.</summary>
        internal static string @ArgumentOutOfRange_UnmanagedMemStreamWrapAround => GetResourceString("ArgumentOutOfRange_UnmanagedMemStreamWrapAround");
        /// <summary>The method cannot be called twice on the same instance.</summary>
        internal static string @InvalidOperation_CalledTwice => GetResourceString("InvalidOperation_CalledTwice");
        /// <summary>Unable to expand length of this stream beyond its capacity.</summary>
        internal static string @IO_FixedCapacity => GetResourceString("IO_FixedCapacity");
        /// <summary>An attempt was made to move the position before the beginning of the stream.</summary>
        internal static string @IO_SeekBeforeBegin => GetResourceString("IO_SeekBeforeBegin");
        /// <summary>Stream was too long.</summary>
        internal static string @IO_StreamTooLong => GetResourceString("IO_StreamTooLong");
        /// <summary>Read an invalid decimal value from the buffer.</summary>
        internal static string @Arg_BadDecimal => GetResourceString("Arg_BadDecimal");
        /// <summary>Accessor does not support reading.</summary>
        internal static string @NotSupported_Reading => GetResourceString("NotSupported_Reading");
        /// <summary>This operation is not supported for an UnmanagedMemoryStream created from a SafeBuffer.</summary>
        internal static string @NotSupported_UmsSafeBuffer => GetResourceString("NotSupported_UmsSafeBuffer");
        /// <summary>Accessor does not support writing.</summary>
        internal static string @NotSupported_Writing => GetResourceString("NotSupported_Writing");
        /// <summary>Stream does not support seeking.</summary>
        internal static string @NotSupported_UnseekableStream => GetResourceString("NotSupported_UnseekableStream");
        /// <summary>Unmanaged memory stream position was beyond the capacity of the stream.</summary>
        internal static string @IndexOutOfRange_UMSPosition => GetResourceString("IndexOutOfRange_UMSPosition");
        /// <summary>ArrayWithOffset: offset exceeds array size.</summary>
        internal static string @IndexOutOfRange_ArrayWithOffset => GetResourceString("IndexOutOfRange_ArrayWithOffset");
        /// <summary>Cannot access a closed accessor.</summary>
        internal static string @ObjectDisposed_ViewAccessorClosed => GetResourceString("ObjectDisposed_ViewAccessorClosed");
        /// <summary>The position may not be greater or equal to the capacity of the accessor.</summary>
        internal static string @ArgumentOutOfRange_PositionLessThanCapacityRequired => GetResourceString("ArgumentOutOfRange_PositionLessThanCapacityRequired");
        /// <summary>Unable to read beyond the end of the stream.</summary>
        internal static string @IO_EOF_ReadBeyondEOF => GetResourceString("IO_EOF_ReadBeyondEOF");
        /// <summary>Attempted to read past the end of the stream.</summary>
        internal static string @Arg_EndOfStreamException => GetResourceString("Arg_EndOfStreamException");
        /// <summary>Cannot access a closed file.</summary>
        internal static string @ObjectDisposed_FileClosed => GetResourceString("ObjectDisposed_FileClosed");
        /// <summary>Search pattern '{0}' cannot contain ".." to move up directories and can be contained only internally in file/directory names, as in "a..b".</summary>
        internal static string @Arg_InvalidSearchPattern => GetResourceString("Arg_InvalidSearchPattern");
        /// <summary>Specified file length was too large for the file system.</summary>
        internal static string @ArgumentOutOfRange_FileLengthTooBig => GetResourceString("ArgumentOutOfRange_FileLengthTooBig");
        /// <summary>'handle' has been disposed or is an invalid handle.</summary>
        internal static string @Argument_InvalidHandle => GetResourceString("Argument_InvalidHandle");
        /// <summary>'handle' has already been bound to the thread pool, or was not opened for asynchronous I/O.</summary>
        internal static string @Argument_AlreadyBoundOrSyncHandle => GetResourceString("Argument_AlreadyBoundOrSyncHandle");
        /// <summary>'preAllocated' is already in use.</summary>
        internal static string @Argument_PreAllocatedAlreadyAllocated => GetResourceString("Argument_PreAllocatedAlreadyAllocated");
        /// <summary>'overlapped' has already been freed.</summary>
        internal static string @Argument_NativeOverlappedAlreadyFree => GetResourceString("Argument_NativeOverlappedAlreadyFree");
        /// <summary>'overlapped' was not allocated by this ThreadPoolBoundHandle instance.</summary>
        internal static string @Argument_NativeOverlappedWrongBoundHandle => GetResourceString("Argument_NativeOverlappedWrongBoundHandle");
        /// <summary>Handle does not support asynchronous operations. The parameters to the FileStream constructor may need to be changed to indicate that the handle was opened synchronously (that is, it was not opened for overlapped I/O).</summary>
        internal static string @Arg_HandleNotAsync => GetResourceString("Arg_HandleNotAsync");
        /// <summary>Path cannot be null.</summary>
        internal static string @ArgumentNull_Path => GetResourceString("ArgumentNull_Path");
        /// <summary>SafeHandle cannot be null.</summary>
        internal static string @ArgumentNull_SafeHandle => GetResourceString("ArgumentNull_SafeHandle");
        /// <summary>Empty path name is not legal.</summary>
        internal static string @Argument_EmptyPath => GetResourceString("Argument_EmptyPath");
        /// <summary>Combining FileMode: {0} with FileAccess: {1} is invalid.</summary>
        internal static string @Argument_InvalidFileModeAndAccessCombo => GetResourceString("Argument_InvalidFileModeAndAccessCombo");
        /// <summary>Append access can be requested only in write-only mode.</summary>
        internal static string @Argument_InvalidAppendMode => GetResourceString("Argument_InvalidAppendMode");
        /// <summary>[Unknown]</summary>
        internal static string @IO_UnknownFileName => GetResourceString("IO_UnknownFileName");
        /// <summary>The OS handle's position is not what FileStream expected. Do not use a handle simultaneously in one FileStream and in Win32 code or another FileStream. This may cause data loss.</summary>
        internal static string @IO_FileStreamHandlePosition => GetResourceString("IO_FileStreamHandlePosition");
        /// <summary>The file is too long. This operation is currently limited to supporting files less than 2 gigabytes in size.</summary>
        internal static string @IO_FileTooLong2GB => GetResourceString("IO_FileTooLong2GB");
        /// <summary>FileStream was asked to open a device that was not a file. For support for devices like 'com1:' or 'lpt1:', call CreateFile, then use the FileStream constructors that take an OS handle as an IntPtr.</summary>
        internal static string @NotSupported_FileStreamOnNonFiles => GetResourceString("NotSupported_FileStreamOnNonFiles");
        /// <summary>BindHandle for ThreadPool failed on this handle.</summary>
        internal static string @IO_BindHandleFailed => GetResourceString("IO_BindHandleFailed");
        /// <summary>Handle does not support synchronous operations. The parameters to the FileStream constructor may need to be changed to indicate that the handle was opened asynchronously (that is, it was opened explicitly for overlapped I/O).</summary>
        internal static string @Arg_HandleNotSync => GetResourceString("Arg_HandleNotSync");
        /// <summary>Unable to truncate data that previously existed in a file opened in Append mode.</summary>
        internal static string @IO_SetLengthAppendTruncate => GetResourceString("IO_SetLengthAppendTruncate");
        /// <summary>Unable seek backward to overwrite data that previously existed in a file opened in Append mode.</summary>
        internal static string @IO_SeekAppendOverwrite => GetResourceString("IO_SeekAppendOverwrite");
        /// <summary>IO operation will not work. Most likely the file will become too long or the handle was not opened to support synchronous IO operations.</summary>
        internal static string @IO_FileTooLongOrHandleNotSync => GetResourceString("IO_FileTooLongOrHandleNotSync");
        /// <summary>The ResourceReader class does not know how to read this version of .resources files.</summary>
        internal static string @Arg_ResourceFileUnsupportedVersion => GetResourceString("Arg_ResourceFileUnsupportedVersion");
        /// <summary>Stream is not a valid resource file.</summary>
        internal static string @Resources_StreamNotValid => GetResourceString("Resources_StreamNotValid");
        /// <summary>Corrupt .resources file. Unable to read resources from this file because of invalid header information. Try regenerating the .resources file.</summary>
        internal static string @BadImageFormat_ResourcesHeaderCorrupted => GetResourceString("BadImageFormat_ResourcesHeaderCorrupted");
        /// <summary>Stream was not readable.</summary>
        internal static string @Argument_StreamNotReadable => GetResourceString("Argument_StreamNotReadable");
        /// <summary>Corrupt .resources file. String length must be non-negative.</summary>
        internal static string @BadImageFormat_NegativeStringLength => GetResourceString("BadImageFormat_NegativeStringLength");
        /// <summary>Corrupt .resources file. The Invalid offset into name section is .</summary>
        internal static string @BadImageFormat_ResourcesNameInvalidOffset => GetResourceString("BadImageFormat_ResourcesNameInvalidOffset");
        /// <summary>Corrupt .resources file.  The specified type doesn't match the available data in the stream.</summary>
        internal static string @BadImageFormat_TypeMismatch => GetResourceString("BadImageFormat_TypeMismatch");
        /// <summary>Corrupt .resources file. The resource name for name index that extends past the end of the stream is</summary>
        internal static string @BadImageFormat_ResourceNameCorrupted_NameIndex => GetResourceString("BadImageFormat_ResourceNameCorrupted_NameIndex");
        /// <summary>Corrupt .resources file. Invalid offset  into data section is</summary>
        internal static string @BadImageFormat_ResourcesDataInvalidOffset => GetResourceString("BadImageFormat_ResourcesDataInvalidOffset");
        /// <summary>Too many bytes in what should have been a 7-bit encoded integer.</summary>
        internal static string @Format_Bad7BitInt => GetResourceString("Format_Bad7BitInt");
        /// <summary>Corrupt .resources file.  The specified type doesn't exist.</summary>
        internal static string @BadImageFormat_InvalidType => GetResourceString("BadImageFormat_InvalidType");
        /// <summary>ResourceReader is closed.</summary>
        internal static string @ResourceReaderIsClosed => GetResourceString("ResourceReaderIsClosed");
        /// <summary>Unable to find manifest resource.</summary>
        internal static string @Arg_MissingManifestResourceException => GetResourceString("Arg_MissingManifestResourceException");
        /// <summary>The keys for this dictionary are missing.</summary>
        internal static string @Serialization_MissingKeys => GetResourceString("Serialization_MissingKeys");
        /// <summary>One of the serialized keys is null.</summary>
        internal static string @Serialization_NullKey => GetResourceString("Serialization_NullKey");
        /// <summary>Mutating a key collection derived from a dictionary is not allowed.</summary>
        internal static string @NotSupported_KeyCollectionSet => GetResourceString("NotSupported_KeyCollectionSet");
        /// <summary>Mutating a value collection derived from a dictionary is not allowed.</summary>
        internal static string @NotSupported_ValueCollectionSet => GetResourceString("NotSupported_ValueCollectionSet");
        /// <summary>MemoryStream's internal buffer cannot be accessed.</summary>
        internal static string @UnauthorizedAccess_MemStreamBuffer => GetResourceString("UnauthorizedAccess_MemStreamBuffer");
        /// <summary>Memory stream is not expandable.</summary>
        internal static string @NotSupported_MemStreamNotExpandable => GetResourceString("NotSupported_MemStreamNotExpandable");
        /// <summary>Stream cannot be null.</summary>
        internal static string @ArgumentNull_Stream => GetResourceString("ArgumentNull_Stream");
        /// <summary>BinaryReader encountered an invalid string length of {0} characters.</summary>
        internal static string @IO_InvalidStringLen_Len => GetResourceString("IO_InvalidStringLen_Len");
        /// <summary>The number of bytes requested does not fit into BinaryReader's internal buffer.</summary>
        internal static string @ArgumentOutOfRange_BinaryReaderFillBuffer => GetResourceString("ArgumentOutOfRange_BinaryReaderFillBuffer");
        /// <summary>Insufficient state to deserialize the object. Missing field '{0}'.</summary>
        internal static string @Serialization_InsufficientDeserializationState => GetResourceString("Serialization_InsufficientDeserializationState");
        /// <summary>The UnitySerializationHolder object is designed to transmit information about other types and is not serializable itself.</summary>
        internal static string @NotSupported_UnitySerHolder => GetResourceString("NotSupported_UnitySerHolder");
        /// <summary>The given module {0} cannot be found within the assembly {1}.</summary>
        internal static string @Serialization_UnableToFindModule => GetResourceString("Serialization_UnableToFindModule");
        /// <summary>Invalid Unity type.</summary>
        internal static string @Argument_InvalidUnity => GetResourceString("Argument_InvalidUnity");
        /// <summary>The handle is invalid.</summary>
        internal static string @InvalidOperation_InvalidHandle => GetResourceString("InvalidOperation_InvalidHandle");
        /// <summary>The named version of this synchronization primitive is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_NamedSynchronizationPrimitives => GetResourceString("PlatformNotSupported_NamedSynchronizationPrimitives");
        /// <summary>The current thread attempted to reacquire a mutex that has reached its maximum acquire count.</summary>
        internal static string @Overflow_MutexReacquireCount => GetResourceString("Overflow_MutexReacquireCount");
        /// <summary>Insufficient state to return the real object.</summary>
        internal static string @Serialization_InsufficientState => GetResourceString("Serialization_InsufficientState");
        /// <summary>Cannot get the member '{0}'.</summary>
        internal static string @Serialization_UnknownMember => GetResourceString("Serialization_UnknownMember");
        /// <summary>The method signature cannot be null.</summary>
        internal static string @Serialization_NullSignature => GetResourceString("Serialization_NullSignature");
        /// <summary>Unknown member type.</summary>
        internal static string @Serialization_MemberTypeNotRecognized => GetResourceString("Serialization_MemberTypeNotRecognized");
        /// <summary>Non existent ParameterInfo. Position bigger than member's parameters length.</summary>
        internal static string @Serialization_BadParameterInfo => GetResourceString("Serialization_BadParameterInfo");
        /// <summary>Serialized member does not have a ParameterInfo.</summary>
        internal static string @Serialization_NoParameterInfo => GetResourceString("Serialization_NoParameterInfo");
        /// <summary>Assembly cannot be null.</summary>
        internal static string @ArgumentNull_Assembly => GetResourceString("ArgumentNull_Assembly");
        /// <summary>The NeutralResourcesLanguageAttribute on the assembly "{0}" specifies an invalid culture name: "{1}".</summary>
        internal static string @Arg_InvalidNeutralResourcesLanguage_Asm_Culture => GetResourceString("Arg_InvalidNeutralResourcesLanguage_Asm_Culture");
        /// <summary>The NeutralResourcesLanguageAttribute specifies an invalid or unrecognized ultimate resource fallback location: "{0}".</summary>
        internal static string @Arg_InvalidNeutralResourcesLanguage_FallbackLoc => GetResourceString("Arg_InvalidNeutralResourcesLanguage_FallbackLoc");
        /// <summary>Satellite contract version attribute on the assembly '{0}' specifies an invalid version: {1}.</summary>
        internal static string @Arg_InvalidSatelliteContract_Asm_Ver => GetResourceString("Arg_InvalidSatelliteContract_Asm_Ver");
        /// <summary>Type parameter must refer to a subclass of ResourceSet.</summary>
        internal static string @Arg_ResMgrNotResSet => GetResourceString("Arg_ResMgrNotResSet");
        /// <summary>Corrupt .resources file. A resource name extends past the end of the stream.</summary>
        internal static string @BadImageFormat_ResourceNameCorrupted => GetResourceString("BadImageFormat_ResourceNameCorrupted");
        /// <summary>Corrupt .resources file. Resource name extends past the end of the file.</summary>
        internal static string @BadImageFormat_ResourcesNameTooLong => GetResourceString("BadImageFormat_ResourcesNameTooLong");
        /// <summary>'{0}': ResourceSet derived classes must provide a constructor that takes a String file name and a constructor that takes a Stream.</summary>
        internal static string @InvalidOperation_ResMgrBadResSet_Type => GetResourceString("InvalidOperation_ResMgrBadResSet_Type");
        /// <summary>Resource '{0}' was not a Stream - call GetObject instead.</summary>
        internal static string @InvalidOperation_ResourceNotStream_Name => GetResourceString("InvalidOperation_ResourceNotStream_Name");
        /// <summary>A case-insensitive lookup for resource file "{0}" in assembly "{1}" found multiple entries. Remove the duplicates or specify the exact case.</summary>
        internal static string @MissingManifestResource_MultipleBlobs => GetResourceString("MissingManifestResource_MultipleBlobs");
        /// <summary>Could not find the resource "{0}" among the resources {2} embedded in the assembly "{1}", nor among the resources in any satellite assemblies for the specified culture. Perhaps the resources were embedded with an incorrect name.</summary>
        internal static string @MissingManifestResource_NoNeutralAsm => GetResourceString("MissingManifestResource_NoNeutralAsm");
        /// <summary>Could not find any resources appropriate for the specified culture (or the neutral culture) on disk.</summary>
        internal static string @MissingManifestResource_NoNeutralDisk => GetResourceString("MissingManifestResource_NoNeutralDisk");
        /// <summary>Unable to open Package Resource Index.</summary>
        internal static string @MissingManifestResource_NoPRIresources => GetResourceString("MissingManifestResource_NoPRIresources");
        /// <summary>Unable to load resources for resource file "{0}" in package "{1}".</summary>
        internal static string @MissingManifestResource_ResWFileNotLoaded => GetResourceString("MissingManifestResource_ResWFileNotLoaded");
        /// <summary>The satellite assembly named "{1}" for fallback culture "{0}" either could not be found or could not be loaded. This is generally a setup problem. Please consider reinstalling or repairing the application.</summary>
        internal static string @MissingSatelliteAssembly_Culture_Name => GetResourceString("MissingSatelliteAssembly_Culture_Name");
        /// <summary>Resource lookup fell back to the ultimate fallback resources in a satellite assembly, but that satellite either was not found or could not be loaded. Please consider reinstalling or repairing the application.</summary>
        internal static string @MissingSatelliteAssembly_Default => GetResourceString("MissingSatelliteAssembly_Default");
        /// <summary>Delegates that are not of type MulticastDelegate may not be combined.</summary>
        internal static string @Multicast_Combine => GetResourceString("Multicast_Combine");
        /// <summary>Found an obsolete .resources file in assembly '{0}'. Rebuild that .resources file then rebuild that assembly.</summary>
        internal static string @NotSupported_ObsoleteResourcesFile => GetResourceString("NotSupported_ObsoleteResourcesFile");
        /// <summary>Cannot read resources that depend on serialization.</summary>
        internal static string @NotSupported_ResourceObjectSerialization => GetResourceString("NotSupported_ResourceObjectSerialization");
        /// <summary>Cannot access a closed resource set.</summary>
        internal static string @ObjectDisposed_ResourceSet => GetResourceString("ObjectDisposed_ResourceSet");
        /// <summary>The specified resource name "{0}" does not exist in the resource file.</summary>
        internal static string @Arg_ResourceNameNotExist => GetResourceString("Arg_ResourceNameNotExist");
        /// <summary>Corrupt .resources file.  The specified data length '{0}' is not a valid position in the stream.</summary>
        internal static string @BadImageFormat_ResourceDataLengthInvalid => GetResourceString("BadImageFormat_ResourceDataLengthInvalid");
        /// <summary>Corrupt .resources file. String for name index '{0}' extends past the end of the file.</summary>
        internal static string @BadImageFormat_ResourcesIndexTooLong => GetResourceString("BadImageFormat_ResourcesIndexTooLong");
        /// <summary>Resource '{0}' was not a String - call GetObject instead.</summary>
        internal static string @InvalidOperation_ResourceNotString_Name => GetResourceString("InvalidOperation_ResourceNotString_Name");
        /// <summary>Resource was of type '{0}' instead of String - call GetObject instead.</summary>
        internal static string @InvalidOperation_ResourceNotString_Type => GetResourceString("InvalidOperation_ResourceNotString_Type");
        /// <summary>This .resources file should not be read with this reader. The resource reader type is "{0}".</summary>
        internal static string @NotSupported_WrongResourceReader_Type => GetResourceString("NotSupported_WrongResourceReader_Type");
        /// <summary>Type must derive from Delegate.</summary>
        internal static string @Arg_MustBeDelegate => GetResourceString("Arg_MustBeDelegate");
        /// <summary>Serialization of global methods (including implicit serialization via the use of asynchronous delegates) is not supported.</summary>
        internal static string @NotSupported_GlobalMethodSerialization => GetResourceString("NotSupported_GlobalMethodSerialization");
        /// <summary>This operation is invalid on overlapping buffers.</summary>
        internal static string @InvalidOperation_SpanOverlappedOperation => GetResourceString("InvalidOperation_SpanOverlappedOperation");
        /// <summary>DelegateSerializationHolder objects are designed to represent a delegate during serialization and are not serializable themselves.</summary>
        internal static string @NotSupported_DelegateSerHolderSerial => GetResourceString("NotSupported_DelegateSerHolderSerial");
        /// <summary>The delegate cannot be serialized properly due to missing metadata for the target method.</summary>
        internal static string @DelegateSer_InsufficientMetadata => GetResourceString("DelegateSer_InsufficientMetadata");
        /// <summary>Uninitialized Strings cannot be created.</summary>
        internal static string @Argument_NoUninitializedStrings => GetResourceString("Argument_NoUninitializedStrings");
        /// <summary>totalSize is too large. For more information about setting the maximum size, see \"Latency Modes\" in http://go.microsoft.com/fwlink/?LinkId=522706.</summary>
        internal static string @ArgumentOutOfRangeException_NoGCRegionSizeTooLarge => GetResourceString("ArgumentOutOfRangeException_NoGCRegionSizeTooLarge");
        /// <summary>The NoGCRegion mode was already in progress.</summary>
        internal static string @InvalidOperationException_AlreadyInNoGCRegion => GetResourceString("InvalidOperationException_AlreadyInNoGCRegion");
        /// <summary>Allocated memory exceeds specified memory for NoGCRegion mode.</summary>
        internal static string @InvalidOperationException_NoGCRegionAllocationExceeded => GetResourceString("InvalidOperationException_NoGCRegionAllocationExceeded");
        /// <summary>Garbage collection was induced in NoGCRegion mode.</summary>
        internal static string @InvalidOperationException_NoGCRegionInduced => GetResourceString("InvalidOperationException_NoGCRegionInduced");
        /// <summary>NoGCRegion mode must be set.</summary>
        internal static string @InvalidOperationException_NoGCRegionNotInProgress => GetResourceString("InvalidOperationException_NoGCRegionNotInProgress");
        /// <summary>The NoGCRegion mode is in progress. End it and then set a different mode.</summary>
        internal static string @InvalidOperation_SetLatencyModeNoGC => GetResourceString("InvalidOperation_SetLatencyModeNoGC");
        /// <summary>This API is not available when the concurrent GC is enabled.</summary>
        internal static string @InvalidOperation_NotWithConcurrentGC => GetResourceString("InvalidOperation_NotWithConcurrentGC");
        /// <summary>Thread is running or terminated; it cannot restart.</summary>
        internal static string @ThreadState_AlreadyStarted => GetResourceString("ThreadState_AlreadyStarted");
        /// <summary>Thread is dead; priority cannot be accessed.</summary>
        internal static string @ThreadState_Dead_Priority => GetResourceString("ThreadState_Dead_Priority");
        /// <summary>Thread is dead; state cannot be accessed.</summary>
        internal static string @ThreadState_Dead_State => GetResourceString("ThreadState_Dead_State");
        /// <summary>Thread has not been started.</summary>
        internal static string @ThreadState_NotStarted => GetResourceString("ThreadState_NotStarted");
        /// <summary>Unable to set thread priority.</summary>
        internal static string @ThreadState_SetPriorityFailed => GetResourceString("ThreadState_SetPriorityFailed");
        /// <summary>Object fields may not be properly initialized.</summary>
        internal static string @Serialization_InvalidFieldState => GetResourceString("Serialization_InvalidFieldState");
        /// <summary>OnDeserialization method was called while the object was not being deserialized.</summary>
        internal static string @Serialization_InvalidOnDeser => GetResourceString("Serialization_InvalidOnDeser");
        /// <summary>Cannot create an abstract class.</summary>
        internal static string @Acc_CreateAbst => GetResourceString("Acc_CreateAbst");
        /// <summary>Cannot create a type for which Type.ContainsGenericParameters is true.</summary>
        internal static string @Acc_CreateGeneric => GetResourceString("Acc_CreateGeneric");
        /// <summary>Value was invalid.</summary>
        internal static string @Argument_InvalidValue => GetResourceString("Argument_InvalidValue");
        /// <summary>Cannot create uninitialized instances of types requiring managed activation.</summary>
        internal static string @NotSupported_ManagedActivation => GetResourceString("NotSupported_ManagedActivation");
        /// <summary>ResourceManager method '{0}' is not supported when reading from .resw resource files.</summary>
        internal static string @PlatformNotSupported_ResourceManager_ResWFileUnsupportedMethod => GetResourceString("PlatformNotSupported_ResourceManager_ResWFileUnsupportedMethod");
        /// <summary>ResourceManager property '{0}' is not supported when reading from .resw resource files.</summary>
        internal static string @PlatformNotSupported_ResourceManager_ResWFileUnsupportedProperty => GetResourceString("PlatformNotSupported_ResourceManager_ResWFileUnsupportedProperty");
        /// <summary>Object cannot be cast to DBNull.</summary>
        internal static string @InvalidCast_DBNull => GetResourceString("InvalidCast_DBNull");
        /// <summary>This feature is not currently implemented.</summary>
        internal static string @NotSupported_NYI => GetResourceString("NotSupported_NYI");
        /// <summary>The corresponding delegate has been garbage collected. Please make sure the delegate is still referenced by managed code when you are using the marshalled native function pointer.</summary>
        internal static string @Delegate_GarbageCollected => GetResourceString("Delegate_GarbageCollected");
        /// <summary>Ambiguous match found.</summary>
        internal static string @Arg_AmbiguousMatchException => GetResourceString("Arg_AmbiguousMatchException");
        /// <summary>ChangeType operation is not supported.</summary>
        internal static string @NotSupported_ChangeType => GetResourceString("NotSupported_ChangeType");
        /// <summary>Array may not be empty.</summary>
        internal static string @Arg_EmptyArray => GetResourceString("Arg_EmptyArray");
        /// <summary>Member not found.</summary>
        internal static string @MissingMember => GetResourceString("MissingMember");
        /// <summary>Field not found.</summary>
        internal static string @MissingField => GetResourceString("MissingField");
        /// <summary>Object cannot be cast from DBNull to other types.</summary>
        internal static string @InvalidCast_FromDBNull => GetResourceString("InvalidCast_FromDBNull");
        /// <summary>Only one DBNull instance may exist, and calls to DBNull deserialization methods are not allowed.</summary>
        internal static string @NotSupported_DBNullSerial => GetResourceString("NotSupported_DBNullSerial");
        /// <summary>The invoked member is not supported in a dynamic assembly.</summary>
        internal static string @NotSupported_DynamicAssembly => GetResourceString("NotSupported_DynamicAssembly");
        /// <summary>The serialized Capacity property of StringBuilder must be positive, less than or equal to MaxCapacity and greater than or equal to the String length.</summary>
        internal static string @Serialization_StringBuilderCapacity => GetResourceString("Serialization_StringBuilderCapacity");
        /// <summary>The serialized MaxCapacity property of StringBuilder must be positive and greater than or equal to the String length.</summary>
        internal static string @Serialization_StringBuilderMaxCapacity => GetResourceString("Serialization_StringBuilderMaxCapacity");
        /// <summary>Remoting is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_Remoting => GetResourceString("PlatformNotSupported_Remoting");
        /// <summary>Strong-name signing is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_StrongNameSigning => GetResourceString("PlatformNotSupported_StrongNameSigning");
        /// <summary>Invalid serialized DateTime data. Unable to find 'ticks' or 'dateData'.</summary>
        internal static string @Serialization_MissingDateTimeData => GetResourceString("Serialization_MissingDateTimeData");
        /// <summary>The values for this dictionary are missing.</summary>
        internal static string @Serialization_MissingValues => GetResourceString("Serialization_MissingValues");
        /// <summary>Invalid serialized DateTime data. Ticks must be between DateTime.MinValue.Ticks and DateTime.MaxValue.Ticks.</summary>
        internal static string @Serialization_DateTimeTicksOutOfRange => GetResourceString("Serialization_DateTimeTicksOutOfRange");
        /// <summary>Code to support feature '{0}' was removed during publishing. If this is in error, update the project configuration to not disable feature '{0}'.</summary>
        internal static string @FeatureRemoved_Message => GetResourceString("FeatureRemoved_Message");
        /// <summary>The ANSI string passed in could not be converted from the default ANSI code page to Unicode.</summary>
        internal static string @Arg_InvalidANSIString => GetResourceString("Arg_InvalidANSIString");
        /// <summary>The string must be null-terminated.</summary>
        internal static string @Arg_MustBeNullTerminatedString => GetResourceString("Arg_MustBeNullTerminatedString");
        /// <summary>ArgIterator is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_ArgIterator => GetResourceString("PlatformNotSupported_ArgIterator");
        /// <summary>Type had been unloaded.</summary>
        internal static string @Arg_TypeUnloadedException => GetResourceString("Arg_TypeUnloadedException");
        /// <summary>Value was either too large or too small for a Currency.</summary>
        internal static string @Overflow_Currency => GetResourceString("Overflow_Currency");
        /// <summary>Secure binary serialization is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_SecureBinarySerialization => GetResourceString("PlatformNotSupported_SecureBinarySerialization");
        /// <summary>An IntPtr or UIntPtr with an eight byte value cannot be deserialized on a machine with a four byte word size.</summary>
        internal static string @Serialization_InvalidPtrValue => GetResourceString("Serialization_InvalidPtrValue");
        /// <summary>The keys and values arrays have different sizes.</summary>
        internal static string @Serialization_KeyValueDifferentSizes => GetResourceString("Serialization_KeyValueDifferentSizes");
        /// <summary>Abstract event source must not declare event methods ({0} with ID {1}).</summary>
        internal static string @EventSource_AbstractMustNotDeclareEventMethods => GetResourceString("EventSource_AbstractMustNotDeclareEventMethods");
        /// <summary>Abstract event source must not declare {0} nested type.</summary>
        internal static string @EventSource_AbstractMustNotDeclareKTOC => GetResourceString("EventSource_AbstractMustNotDeclareKTOC");
        /// <summary>Getting out of bounds during scalar addition.</summary>
        internal static string @EventSource_AddScalarOutOfRange => GetResourceString("EventSource_AddScalarOutOfRange");
        /// <summary>Bad Hexidecimal digit "{0}".</summary>
        internal static string @EventSource_BadHexDigit => GetResourceString("EventSource_BadHexDigit");
        /// <summary>Channel {0} does not match event channel value {1}.</summary>
        internal static string @EventSource_ChannelTypeDoesNotMatchEventChannelValue => GetResourceString("EventSource_ChannelTypeDoesNotMatchEventChannelValue");
        /// <summary>Data descriptors are out of range.</summary>
        internal static string @EventSource_DataDescriptorsOutOfRange => GetResourceString("EventSource_DataDescriptorsOutOfRange");
        /// <summary>Multiple definitions for string "{0}".</summary>
        internal static string @EventSource_DuplicateStringKey => GetResourceString("EventSource_DuplicateStringKey");
        /// <summary>The type of {0} is not expected in {1}.</summary>
        internal static string @EventSource_EnumKindMismatch => GetResourceString("EventSource_EnumKindMismatch");
        /// <summary>Must have an even number of Hexidecimal digits.</summary>
        internal static string @EventSource_EvenHexDigits => GetResourceString("EventSource_EvenHexDigits");
        /// <summary>Channel {0} has a value of {1} which is outside the legal range (16-254).</summary>
        internal static string @EventSource_EventChannelOutOfRange => GetResourceString("EventSource_EventChannelOutOfRange");
        /// <summary>Event {0} has ID {1} which is already in use.</summary>
        internal static string @EventSource_EventIdReused => GetResourceString("EventSource_EventIdReused");
        /// <summary>Event {0} (with ID {1}) has a non-default opcode but not a task.</summary>
        internal static string @EventSource_EventMustHaveTaskIfNonDefaultOpcode => GetResourceString("EventSource_EventMustHaveTaskIfNonDefaultOpcode");
        /// <summary>Event method {0} (with ID {1}) is an explicit interface method implementation. Re-write method as implicit implementation.</summary>
        internal static string @EventSource_EventMustNotBeExplicitImplementation => GetResourceString("EventSource_EventMustNotBeExplicitImplementation");
        /// <summary>Event {0} (with ID {1}) has a name that is not the concatenation of its task name and opcode.</summary>
        internal static string @EventSource_EventNameDoesNotEqualTaskPlusOpcode => GetResourceString("EventSource_EventNameDoesNotEqualTaskPlusOpcode");
        /// <summary>Event name {0} used more than once.  If you wish to overload a method, the overloaded method should have a NonEvent attribute.</summary>
        internal static string @EventSource_EventNameReused => GetResourceString("EventSource_EventNameReused");
        /// <summary>Event {0} was called with {1} argument(s), but it is defined with {2} parameter(s).</summary>
        internal static string @EventSource_EventParametersMismatch => GetResourceString("EventSource_EventParametersMismatch");
        /// <summary>An instance of EventSource with Guid {0} already exists.</summary>
        internal static string @EventSource_EventSourceGuidInUse => GetResourceString("EventSource_EventSourceGuidInUse");
        /// <summary>The payload for a single event is too large.</summary>
        internal static string @EventSource_EventTooBig => GetResourceString("EventSource_EventTooBig");
        /// <summary>Event {0} specifies an Admin channel {1}. It must specify a Message property.</summary>
        internal static string @EventSource_EventWithAdminChannelMustHaveMessage => GetResourceString("EventSource_EventWithAdminChannelMustHaveMessage");
        /// <summary>Keyword {0} has a value of {1} which is outside the legal range (0-0x0000080000000000).</summary>
        internal static string @EventSource_IllegalKeywordsValue => GetResourceString("EventSource_IllegalKeywordsValue");
        /// <summary>Opcode {0} has a value of {1} which is outside the legal range (11-238).</summary>
        internal static string @EventSource_IllegalOpcodeValue => GetResourceString("EventSource_IllegalOpcodeValue");
        /// <summary>Task {0} has a value of {1} which is outside the legal range (1-65535).</summary>
        internal static string @EventSource_IllegalTaskValue => GetResourceString("EventSource_IllegalTaskValue");
        /// <summary>Illegal value "{0}" (prefix strings with @ to indicate a literal string).</summary>
        internal static string @EventSource_IllegalValue => GetResourceString("EventSource_IllegalValue");
        /// <summary>Incorrectly-authored TypeInfo - a type should be serialized as one field or as one group</summary>
        internal static string @EventSource_IncorrentlyAuthoredTypeInfo => GetResourceString("EventSource_IncorrentlyAuthoredTypeInfo");
        /// <summary>Invalid command value.</summary>
        internal static string @EventSource_InvalidCommand => GetResourceString("EventSource_InvalidCommand");
        /// <summary>Can't specify both etw event format flags.</summary>
        internal static string @EventSource_InvalidEventFormat => GetResourceString("EventSource_InvalidEventFormat");
        /// <summary>Keywords {0} and {1} are defined with the same value ({2}).</summary>
        internal static string @EventSource_KeywordCollision => GetResourceString("EventSource_KeywordCollision");
        /// <summary>Value {0} for keyword {1} needs to be a power of 2.</summary>
        internal static string @EventSource_KeywordNeedPowerOfTwo => GetResourceString("EventSource_KeywordNeedPowerOfTwo");
        /// <summary>Creating an EventListener inside a EventListener callback.</summary>
        internal static string @EventSource_ListenerCreatedInsideCallback => GetResourceString("EventSource_ListenerCreatedInsideCallback");
        /// <summary>Listener not found.</summary>
        internal static string @EventSource_ListenerNotFound => GetResourceString("EventSource_ListenerNotFound");
        /// <summary>An error occurred when writing to a listener.</summary>
        internal static string @EventSource_ListenerWriteFailure => GetResourceString("EventSource_ListenerWriteFailure");
        /// <summary>Attempt to define more than the maximum limit of 8 channels for a provider.</summary>
        internal static string @EventSource_MaxChannelExceeded => GetResourceString("EventSource_MaxChannelExceeded");
        /// <summary>Event {0} was assigned event ID {1} but {2} was passed to WriteEvent.</summary>
        internal static string @EventSource_MismatchIdToWriteEvent => GetResourceString("EventSource_MismatchIdToWriteEvent");
        /// <summary>The Guid of an EventSource must be non zero.</summary>
        internal static string @EventSource_NeedGuid => GetResourceString("EventSource_NeedGuid");
        /// <summary>The name of an EventSource must not be null.</summary>
        internal static string @EventSource_NeedName => GetResourceString("EventSource_NeedName");
        /// <summary>Event IDs must be positive integers.</summary>
        internal static string @EventSource_NeedPositiveId => GetResourceString("EventSource_NeedPositiveId");
        /// <summary>No Free Buffers available from the operating system (e.g. event rate too fast).</summary>
        internal static string @EventSource_NoFreeBuffers => GetResourceString("EventSource_NoFreeBuffers");
        /// <summary>The API supports only anonymous types or types decorated with the EventDataAttribute. Non-compliant type: {0} dataType.</summary>
        internal static string @EventSource_NonCompliantTypeError => GetResourceString("EventSource_NonCompliantTypeError");
        /// <summary>EventSource expects the first parameter of the Event method to be of type Guid and to be named "relatedActivityId" when calling WriteEventWithRelatedActivityId.</summary>
        internal static string @EventSource_NoRelatedActivityId => GetResourceString("EventSource_NoRelatedActivityId");
        /// <summary>Arrays of Binary are not supported.</summary>
        internal static string @EventSource_NotSupportedArrayOfBinary => GetResourceString("EventSource_NotSupportedArrayOfBinary");
        /// <summary>Arrays of Nil are not supported.</summary>
        internal static string @EventSource_NotSupportedArrayOfNil => GetResourceString("EventSource_NotSupportedArrayOfNil");
        /// <summary>Arrays of null-terminated string are not supported.</summary>
        internal static string @EventSource_NotSupportedArrayOfNullTerminatedString => GetResourceString("EventSource_NotSupportedArrayOfNullTerminatedString");
        /// <summary>Enumerables of custom-serialized data are not supported</summary>
        internal static string @EventSource_NotSupportedCustomSerializedData => GetResourceString("EventSource_NotSupportedCustomSerializedData");
        /// <summary>Nested arrays/enumerables are not supported.</summary>
        internal static string @EventSource_NotSupportedNestedArraysEnums => GetResourceString("EventSource_NotSupportedNestedArraysEnums");
        /// <summary>Null passed as a event argument.</summary>
        internal static string @EventSource_NullInput => GetResourceString("EventSource_NullInput");
        /// <summary>Opcodes {0} and {1} are defined with the same value ({2}).</summary>
        internal static string @EventSource_OpcodeCollision => GetResourceString("EventSource_OpcodeCollision");
        /// <summary>Pins are out of range.</summary>
        internal static string @EventSource_PinArrayOutOfRange => GetResourceString("EventSource_PinArrayOutOfRange");
        /// <summary>Recursive type definition is not supported.</summary>
        internal static string @EventSource_RecursiveTypeDefinition => GetResourceString("EventSource_RecursiveTypeDefinition");
        /// <summary>Bit position in AllKeywords ({0}) must equal the command argument named "EtwSessionKeyword" ({1}).</summary>
        internal static string @EventSource_SessionIdError => GetResourceString("EventSource_SessionIdError");
        /// <summary>An event with stop suffix must follow a corresponding event with a start suffix.</summary>
        internal static string @EventSource_StopsFollowStarts => GetResourceString("EventSource_StopsFollowStarts");
        /// <summary>Tasks {0} and {1} are defined with the same value ({2}).</summary>
        internal static string @EventSource_TaskCollision => GetResourceString("EventSource_TaskCollision");
        /// <summary>Event {0} (with ID {1}) has the same task/opcode pair as event {2} (with ID {3}).</summary>
        internal static string @EventSource_TaskOpcodePairReused => GetResourceString("EventSource_TaskOpcodePairReused");
        /// <summary>Too many arguments.</summary>
        internal static string @EventSource_TooManyArgs => GetResourceString("EventSource_TooManyArgs");
        /// <summary>Too many fields in structure.</summary>
        internal static string @EventSource_TooManyFields => GetResourceString("EventSource_TooManyFields");
        /// <summary>EventSource({0}, {1})</summary>
        internal static string @EventSource_ToString => GetResourceString("EventSource_ToString");
        /// <summary>There must be an even number of trait strings (they are key-value pairs).</summary>
        internal static string @EventSource_TraitEven => GetResourceString("EventSource_TraitEven");
        /// <summary>Event source types must be sealed or abstract.</summary>
        internal static string @EventSource_TypeMustBeSealedOrAbstract => GetResourceString("EventSource_TypeMustBeSealedOrAbstract");
        /// <summary>Event source types must derive from EventSource.</summary>
        internal static string @EventSource_TypeMustDeriveFromEventSource => GetResourceString("EventSource_TypeMustDeriveFromEventSource");
        /// <summary>Use of undefined channel value {0} for event {1}.</summary>
        internal static string @EventSource_UndefinedChannel => GetResourceString("EventSource_UndefinedChannel");
        /// <summary>Use of undefined keyword value {0} for event {1}.</summary>
        internal static string @EventSource_UndefinedKeyword => GetResourceString("EventSource_UndefinedKeyword");
        /// <summary>Use of undefined opcode value {0} for event {1}.</summary>
        internal static string @EventSource_UndefinedOpcode => GetResourceString("EventSource_UndefinedOpcode");
        /// <summary>Unknown ETW trait "{0}".</summary>
        internal static string @EventSource_UnknownEtwTrait => GetResourceString("EventSource_UnknownEtwTrait");
        /// <summary>Unsupported type {0} in event source.</summary>
        internal static string @EventSource_UnsupportedEventTypeInManifest => GetResourceString("EventSource_UnsupportedEventTypeInManifest");
        /// <summary>Event {0} specifies an illegal or unsupported formatting message ("{1}").</summary>
        internal static string @EventSource_UnsupportedMessageProperty => GetResourceString("EventSource_UnsupportedMessageProperty");
        /// <summary>The parameters to the Event method do not match the parameters to the WriteEvent method. This may cause the event to be displayed incorrectly.</summary>
        internal static string @EventSource_VarArgsParameterMismatch => GetResourceString("EventSource_VarArgsParameterMismatch");
        /// <summary>Stream was not writable.</summary>
        internal static string @Argument_StreamNotWritable => GetResourceString("Argument_StreamNotWritable");
        /// <summary>Unicode surrogate characters must be written out as pairs together in the same call, not individually. Consider passing in a character array instead.</summary>
        internal static string @Arg_SurrogatesNotAllowedAsSingleChar => GetResourceString("Arg_SurrogatesNotAllowedAsSingleChar");
        /// <summary>'{0}' field specified was not found.</summary>
        internal static string @CustomAttributeFormat_InvalidFieldFail => GetResourceString("CustomAttributeFormat_InvalidFieldFail");
        /// <summary>'{0}' property specified was not found.</summary>
        internal static string @CustomAttributeFormat_InvalidPropertyFail => GetResourceString("CustomAttributeFormat_InvalidPropertyFail");
        /// <summary>Equals() on Span and ReadOnlySpan is not supported. Use operator== instead.</summary>
        internal static string @NotSupported_CannotCallEqualsOnSpan => GetResourceString("NotSupported_CannotCallEqualsOnSpan");
        /// <summary>GetHashCode() on Span and ReadOnlySpan is not supported.</summary>
        internal static string @NotSupported_CannotCallGetHashCodeOnSpan => GetResourceString("NotSupported_CannotCallGetHashCodeOnSpan");
        /// <summary>Destination is too short.</summary>
        internal static string @Argument_DestinationTooShort => GetResourceString("Argument_DestinationTooShort");
        /// <summary>Cannot use type '{0}'. Only value types without pointers or references are supported.</summary>
        internal static string @Argument_InvalidTypeWithPointersNotSupported => GetResourceString("Argument_InvalidTypeWithPointersNotSupported");
        /// <summary>Overlapping spans have mismatching alignment.</summary>
        internal static string @Argument_OverlapAlignmentMismatch => GetResourceString("Argument_OverlapAlignmentMismatch");
        /// <summary>Array.ConstrainedCopy will only work on array types that are provably compatible, without any form of boxing, unboxing, widening, or casting of each array element.  Change the array types (i.e., copy a Derived[] to a Base[]), or use a mitigation strategy i ...</summary>
        internal static string @ArrayTypeMismatch_ConstrainedCopy => GetResourceString("ArrayTypeMismatch_ConstrainedCopy");
        /// <summary>Dll was not found.</summary>
        internal static string @Arg_DllNotFoundException => GetResourceString("Arg_DllNotFoundException");
        /// <summary>Unable to load DLL '{0}': The specified module could not be found.</summary>
        internal static string @Arg_DllNotFoundExceptionParameterized => GetResourceString("Arg_DllNotFoundExceptionParameterized");
        /// <summary>Attempted to access a drive that is not available.</summary>
        internal static string @Arg_DriveNotFoundException => GetResourceString("Arg_DriveNotFoundException");
        /// <summary>Type could not be marshaled because the length of an embedded array instance does not match the declared length in the layout.</summary>
        internal static string @WrongSizeArrayInNStruct => GetResourceString("WrongSizeArrayInNStruct");
        /// <summary>Cannot marshal: Encountered unmappable character.</summary>
        internal static string @Arg_InteropMarshalUnmappableChar => GetResourceString("Arg_InteropMarshalUnmappableChar");
        /// <summary>Marshaling directives are invalid.</summary>
        internal static string @Arg_MarshalDirectiveException => GetResourceString("Arg_MarshalDirectiveException");
        /// <summary>No value exists with that name.</summary>
        internal static string @Arg_RegSubKeyValueAbsent => GetResourceString("Arg_RegSubKeyValueAbsent");
        /// <summary>Registry value names should not be greater than 16,383 characters.</summary>
        internal static string @Arg_RegValStrLenBug => GetResourceString("Arg_RegValStrLenBug");
        /// <summary>Serializing delegates is not supported on this platform.</summary>
        internal static string @Serialization_DelegatesNotSupported => GetResourceString("Serialization_DelegatesNotSupported");
        /// <summary>Cannot create an instance of {0} as it is an open type.</summary>
        internal static string @Arg_OpenType => GetResourceString("Arg_OpenType");
        /// <summary>AssemblyName.GetAssemblyName() is not supported on this platform.</summary>
        internal static string @Arg_PlatformNotSupported_AssemblyName_GetAssemblyName => GetResourceString("Arg_PlatformNotSupported_AssemblyName_GetAssemblyName");
        /// <summary>Cannot create arrays of open type.</summary>
        internal static string @NotSupported_OpenType => GetResourceString("NotSupported_OpenType");
        /// <summary>Cannot create arrays of ByRef-like values.</summary>
        internal static string @NotSupported_ByRefLikeArray => GetResourceString("NotSupported_ByRefLikeArray");
        /// <summary>at</summary>
        internal static string @Word_At => GetResourceString("Word_At");
        /// <summary>--- End of stack trace from previous location where exception was thrown ---</summary>
        internal static string @StackTrace_EndStackTraceFromPreviousThrow => GetResourceString("StackTrace_EndStackTraceFromPreviousThrow");
        /// <summary>The given assembly name or codebase was invalid</summary>
        internal static string @InvalidAssemblyName => GetResourceString("InvalidAssemblyName");
        /// <summary>Must be an array type.</summary>
        internal static string @Argument_HasToBeArrayClass => GetResourceString("Argument_HasToBeArrayClass");
        /// <summary>Left to right characters may not be mixed with right to left characters in IDN labels.</summary>
        internal static string @Argument_IdnBadBidi => GetResourceString("Argument_IdnBadBidi");
        /// <summary>IDN labels must be between 1 and 63 characters long.</summary>
        internal static string @Argument_IdnBadLabelSize => GetResourceString("Argument_IdnBadLabelSize");
        /// <summary>IDN names must be between 1 and {0} characters long.</summary>
        internal static string @Argument_IdnBadNameSize => GetResourceString("Argument_IdnBadNameSize");
        /// <summary>Invalid IDN encoded string.</summary>
        internal static string @Argument_IdnBadPunycode => GetResourceString("Argument_IdnBadPunycode");
        /// <summary>Label contains character '{0}' not allowed with UseStd3AsciiRules</summary>
        internal static string @Argument_IdnBadStd3 => GetResourceString("Argument_IdnBadStd3");
        /// <summary>Decoded string is not a valid IDN name.</summary>
        internal static string @Argument_IdnIllegalName => GetResourceString("Argument_IdnIllegalName");
        /// <summary>This operation is only valid on generic types.</summary>
        internal static string @InvalidOperation_NotGenericType => GetResourceString("InvalidOperation_NotGenericType");
        /// <summary>This method is not supported on signature types.</summary>
        internal static string @NotSupported_SignatureType => GetResourceString("NotSupported_SignatureType");
        /// <summary>Memory&lt;T&gt; has been disposed.</summary>
        internal static string @MemoryDisposed => GetResourceString("MemoryDisposed");
        /// <summary>Release all references before disposing this instance.</summary>
        internal static string @Memory_OutstandingReferences => GetResourceString("Memory_OutstandingReferences");
        /// <summary>HashCode is a mutable struct and should not be compared with other HashCodes. Use ToHashCode to retrieve the computed hash code.</summary>
        internal static string @HashCode_HashCodeNotSupported => GetResourceString("HashCode_HashCodeNotSupported");
        /// <summary>HashCode is a mutable struct and should not be compared with other HashCodes.</summary>
        internal static string @HashCode_EqualityNotSupported => GetResourceString("HashCode_EqualityNotSupported");
        /// <summary>Cannot write to a closed TextWriter.</summary>
        internal static string @ObjectDisposed_WriterClosed => GetResourceString("ObjectDisposed_WriterClosed");
        /// <summary>Cannot read from a closed TextReader.</summary>
        internal static string @ObjectDisposed_ReaderClosed => GetResourceString("ObjectDisposed_ReaderClosed");
        /// <summary>The read operation returned an invalid length.</summary>
        internal static string @IO_InvalidReadLength => GetResourceString("IO_InvalidReadLength");
        /// <summary>Basepath argument is not fully qualified.</summary>
        internal static string @Arg_BasePathNotFullyQualified => GetResourceString("Arg_BasePathNotFullyQualified");
        /// <summary>Number of elements in source vector is greater than the destination array</summary>
        internal static string @Arg_ElementsInSourceIsGreaterThanDestination => GetResourceString("Arg_ElementsInSourceIsGreaterThanDestination");
        /// <summary>Specified type is not supported</summary>
        internal static string @Arg_TypeNotSupported => GetResourceString("Arg_TypeNotSupported");
        /// <summary>The method was called with a null array argument.</summary>
        internal static string @Arg_NullArgumentNullRef => GetResourceString("Arg_NullArgumentNullRef");
        /// <summary>At least {0} element(s) are expected in the parameter "{1}".</summary>
        internal static string @Arg_InsufficientNumberOfElements => GetResourceString("Arg_InsufficientNumberOfElements");
        /// <summary>The target method returned a null reference.</summary>
        internal static string @NullReference_InvokeNullRefReturned => GetResourceString("NullReference_InvokeNullRefReturned");
        /// <summary>Computer name could not be obtained.</summary>
        internal static string @InvalidOperation_ComputerName => GetResourceString("InvalidOperation_ComputerName");
        /// <summary>Operations that change non-concurrent collections must have exclusive access. A concurrent update was performed on this collection and corrupted its state. The collection's state is no longer correct.</summary>
        internal static string @InvalidOperation_ConcurrentOperationsNotSupported => GetResourceString("InvalidOperation_ConcurrentOperationsNotSupported");
        /// <summary>Failed to set the specified COM apartment state.</summary>
        internal static string @Thread_ApartmentState_ChangeFailed => GetResourceString("Thread_ApartmentState_ChangeFailed");
        /// <summary>Use CompressedStack.(Capture/Run) instead.</summary>
        internal static string @Thread_GetSetCompressedStack_NotSupported => GetResourceString("Thread_GetSetCompressedStack_NotSupported");
        /// <summary>This operation must be performed on the same thread as that represented by the Thread instance.</summary>
        internal static string @Thread_Operation_RequiresCurrentThread => GetResourceString("Thread_Operation_RequiresCurrentThread");
        /// <summary>The SyncRoot property may not be used for the synchronization of concurrent collections.</summary>
        internal static string @ConcurrentCollection_SyncRoot_NotSupported => GetResourceString("ConcurrentCollection_SyncRoot_NotSupported");
        /// <summary>IAsyncResult object did not come from the corresponding async method on this type.</summary>
        internal static string @Arg_WrongAsyncResult => GetResourceString("Arg_WrongAsyncResult");
        /// <summary>EndRead can only be called once for each asynchronous operation.</summary>
        internal static string @InvalidOperation_EndReadCalledMultiple => GetResourceString("InvalidOperation_EndReadCalledMultiple");
        /// <summary>EndWrite can only be called once for each asynchronous operation.</summary>
        internal static string @InvalidOperation_EndWriteCalledMultiple => GetResourceString("InvalidOperation_EndWriteCalledMultiple");
        /// <summary>Either the IAsyncResult object did not come from the corresponding async method on this type, or EndRead was called multiple times with the same IAsyncResult.</summary>
        internal static string @InvalidOperation_WrongAsyncResultOrEndReadCalledMultiple => GetResourceString("InvalidOperation_WrongAsyncResultOrEndReadCalledMultiple");
        /// <summary>Either the IAsyncResult object did not come from the corresponding async method on this type, or EndWrite was called multiple times with the same IAsyncResult.</summary>
        internal static string @InvalidOperation_WrongAsyncResultOrEndWriteCalledMultiple => GetResourceString("InvalidOperation_WrongAsyncResultOrEndWriteCalledMultiple");
        /// <summary>The week parameter must be in the range 1 through 53.</summary>
        internal static string @ArgumentOutOfRange_Week_ISO => GetResourceString("ArgumentOutOfRange_Week_ISO");
        /// <summary>The type serialized in the .resources file was not the same type that the .resources file said it contained. Expected '{0}' but read '{1}'.</summary>
        internal static string @BadImageFormat_ResType_SerBlobMismatch => GetResourceString("BadImageFormat_ResType_SerBlobMismatch");
        /// <summary>Cannot extract a Unicode scalar value from the specified index in the input.</summary>
        internal static string @Argument_CannotExtractScalar => GetResourceString("Argument_CannotExtractScalar");
        /// <summary>Characters following the format symbol must be a number of {0} or less.</summary>
        internal static string @Argument_CannotParsePrecision => GetResourceString("Argument_CannotParsePrecision");
        /// <summary>The 'G' format combined with a precision is not supported.</summary>
        internal static string @Argument_GWithPrecisionNotSupported => GetResourceString("Argument_GWithPrecisionNotSupported");
        /// <summary>Precision cannot be larger than {0}.</summary>
        internal static string @Argument_PrecisionTooLarge => GetResourceString("Argument_PrecisionTooLarge");
        /// <summary>Cannot load hostpolicy library. AssemblyDependencyResolver is currently only supported if the runtime is hosted through hostpolicy library.</summary>
        internal static string @AssemblyDependencyResolver_FailedToLoadHostpolicy => GetResourceString("AssemblyDependencyResolver_FailedToLoadHostpolicy");
        /// <summary>Dependency resolution failed for component {0} with error code {1}. Detailed error: {2}</summary>
        internal static string @AssemblyDependencyResolver_FailedToResolveDependencies => GetResourceString("AssemblyDependencyResolver_FailedToResolveDependencies");
        /// <summary>An action was attempted during deserialization that could lead to a security vulnerability. The action has been aborted.</summary>
        internal static string @Serialization_DangerousDeserialization => GetResourceString("Serialization_DangerousDeserialization");
        /// <summary>An action was attempted during deserialization that could lead to a security vulnerability. The action has been aborted. To allow the action, set the '{0}' AppContext switch to true.</summary>
        internal static string @Serialization_DangerousDeserialization_Switch => GetResourceString("Serialization_DangerousDeserialization_Switch");
        /// <summary>Assembly.LoadFrom with hashValue is not supported.</summary>
        internal static string @NotSupported_AssemblyLoadFromHash => GetResourceString("NotSupported_AssemblyLoadFromHash");
        /// <summary>Bad IL format.</summary>
        internal static string @BadImageFormat_BadILFormat => GetResourceString("BadImageFormat_BadILFormat");
        /// <summary>Length of items must be same as length of keys.</summary>
        internal static string @Argument_SpansMustHaveSameLength => GetResourceString("Argument_SpansMustHaveSameLength");
        /// <summary>Cannot write to a BufferedStream while the read buffer is not empty if the underlying stream is not seekable. Ensure that the stream underlying this BufferedStream can seek or avoid interleaving read and write operations on this BufferedStream.</summary>
        internal static string @NotSupported_CannotWriteToBufferedStreamIfReadBufferCannotBeFlushed => GetResourceString("NotSupported_CannotWriteToBufferedStreamIfReadBufferCannotBeFlushed");
        /// <summary>Found invalid data while decoding.</summary>
        internal static string @GenericInvalidData => GetResourceString("GenericInvalidData");
        /// <summary>Resource type in the ResourceScope enum is going from a more restrictive resource type to a more general one.  From: "{0}"  To: "{1}"</summary>
        internal static string @Argument_ResourceScopeWrongDirection => GetResourceString("Argument_ResourceScopeWrongDirection");
        /// <summary>The type parameter cannot be null when scoping the resource's visibility to Private or Assembly.</summary>
        internal static string @ArgumentNull_TypeRequiredByResourceScope => GetResourceString("ArgumentNull_TypeRequiredByResourceScope");
        /// <summary>Unknown value for the ResourceScope: {0}  Too many resource type bits may be set.</summary>
        internal static string @Argument_BadResourceScopeTypeBits => GetResourceString("Argument_BadResourceScopeTypeBits");
        /// <summary>Unknown value for the ResourceScope: {0}  Too many resource visibility bits may be set.</summary>
        internal static string @Argument_BadResourceScopeVisibilityBits => GetResourceString("Argument_BadResourceScopeVisibilityBits");
        /// <summary>The parameter '{0}' cannot be an empty string.</summary>
        internal static string @net_emptystringcall => GetResourceString("net_emptystringcall");
        /// <summary>ApplicationId cannot have an empty string for the name.</summary>
        internal static string @Argument_EmptyApplicationName => GetResourceString("Argument_EmptyApplicationName");
        /// <summary>FrameworkName is invalid.</summary>
        internal static string @Argument_FrameworkNameInvalid => GetResourceString("Argument_FrameworkNameInvalid");
        /// <summary>FrameworkName version component is invalid.</summary>
        internal static string @Argument_FrameworkNameInvalidVersion => GetResourceString("Argument_FrameworkNameInvalidVersion");
        /// <summary>FrameworkName version component is missing.</summary>
        internal static string @Argument_FrameworkNameMissingVersion => GetResourceString("Argument_FrameworkNameMissingVersion");
        /// <summary>FrameworkName cannot have less than two components or more than three components.</summary>
        internal static string @Argument_FrameworkNameTooShort => GetResourceString("Argument_FrameworkNameTooShort");
        /// <summary>Non-exhaustive switch expression failed to match its input.</summary>
        internal static string @Arg_SwitchExpressionException => GetResourceString("Arg_SwitchExpressionException");
        /// <summary>Attempted to marshal an object across a context boundary.</summary>
        internal static string @Arg_ContextMarshalException => GetResourceString("Arg_ContextMarshalException");
        /// <summary>Attempted to access an unloaded AppDomain.</summary>
        internal static string @Arg_AppDomainUnloadedException => GetResourceString("Arg_AppDomainUnloadedException");
        /// <summary>Unmatched value was {0}.</summary>
        internal static string @SwitchExpressionException_UnmatchedValue => GetResourceString("SwitchExpressionException_UnmatchedValue");
        /// <summary>Support for UTF-7 is disabled. See {0} for more information.</summary>
        internal static string @Encoding_UTF7_Disabled => GetResourceString("Encoding_UTF7_Disabled");
        /// <summary>The body of this method was removed by the AOT compiler because it's not callable.</summary>
        internal static string @NotSupported_BodyRemoved => GetResourceString("NotSupported_BodyRemoved");
        /// <summary>The feature associated with this method was removed.</summary>
        internal static string @NotSupported_FeatureBodyRemoved => GetResourceString("NotSupported_FeatureBodyRemoved");
        /// <summary>The body of this instance method was removed by the AOT compiler. This can happen if the owning type was not seen as allocated by the AOT compiler.</summary>
        internal static string @NotSupported_InstanceBodyRemoved => GetResourceString("NotSupported_InstanceBodyRemoved");
        /// <summary>Object must be of type Half.</summary>
        internal static string @Arg_MustBeHalf => GetResourceString("Arg_MustBeHalf");
        /// <summary>Object must be of type Rune.</summary>
        internal static string @Arg_MustBeRune => GetResourceString("Arg_MustBeRune");
        /// <summary>BinaryFormatter serialization and deserialization are disabled within this application. See https://aka.ms/binaryformatter for more information.</summary>
        internal static string @BinaryFormatter_SerializationDisallowed => GetResourceString("BinaryFormatter_SerializationDisallowed");
        /// <summary>The argv[0] argument cannot include a double quote.</summary>
        internal static string @Argv_IncludeDoubleQuote => GetResourceString("Argv_IncludeDoubleQuote");
        /// <summary>Attempt to update previously set global instance.</summary>
        internal static string @InvalidOperation_ResetGlobalComWrappersInstance => GetResourceString("InvalidOperation_ResetGlobalComWrappersInstance");
        /// <summary>Use of ResourceManager for custom types is disabled. Set the MSBuild Property CustomResourceTypesSupport to true in order to enable it.</summary>
        internal static string @ResourceManager_ReflectionNotAllowed => GetResourceString("ResourceManager_ReflectionNotAllowed");
        /// <summary>COM Interop requires ComWrapper instance registered for marshalling.</summary>
        internal static string @InvalidOperation_ComInteropRequireComWrapperInstance => GetResourceString("InvalidOperation_ComInteropRequireComWrapperInstance");
        /// <summary>Queue empty.</summary>
        internal static string @InvalidOperation_EmptyQueue => GetResourceString("InvalidOperation_EmptyQueue");
        /// <summary>The target file '{0}' is a directory, not a file.</summary>
        internal static string @Arg_FileIsDirectory_Name => GetResourceString("Arg_FileIsDirectory_Name");
        /// <summary>Invalid File or Directory attributes value.</summary>
        internal static string @Arg_InvalidFileAttrs => GetResourceString("Arg_InvalidFileAttrs");
        /// <summary>Second path fragment must not be a drive or UNC name.</summary>
        internal static string @Arg_Path2IsRooted => GetResourceString("Arg_Path2IsRooted");
        /// <summary>Path must not be a drive.</summary>
        internal static string @Arg_PathIsVolume => GetResourceString("Arg_PathIsVolume");
        /// <summary>The stream's length cannot be changed.</summary>
        internal static string @Argument_FileNotResized => GetResourceString("Argument_FileNotResized");
        /// <summary>The directory specified, '{0}', is not a subdirectory of '{1}'.</summary>
        internal static string @Argument_InvalidSubPath => GetResourceString("Argument_InvalidSubPath");
        /// <summary>The specified directory '{0}' cannot be created.</summary>
        internal static string @IO_CannotCreateDirectory => GetResourceString("IO_CannotCreateDirectory");
        /// <summary>Source and destination path must be different.</summary>
        internal static string @IO_SourceDestMustBeDifferent => GetResourceString("IO_SourceDestMustBeDifferent");
        /// <summary>Source and destination path must have identical roots. Move will not work across volumes.</summary>
        internal static string @IO_SourceDestMustHaveSameRoot => GetResourceString("IO_SourceDestMustHaveSameRoot");
        /// <summary>Synchronous operations should not be performed on the UI thread.  Consider wrapping this method in Task.Run.</summary>
        internal static string @IO_SyncOpOnUIThread => GetResourceString("IO_SyncOpOnUIThread");
        /// <summary>Probable I/O race condition detected while copying memory. The I/O package is not thread safe by default. In multithreaded applications, a stream must be accessed in a thread-safe way, such as a thread-safe wrapper returned by TextReader's or TextWriter's  ...</summary>
        internal static string @IndexOutOfRange_IORaceCondition => GetResourceString("IndexOutOfRange_IORaceCondition");
        /// <summary>File encryption is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_FileEncryption => GetResourceString("PlatformNotSupported_FileEncryption");

    }
}
#endif