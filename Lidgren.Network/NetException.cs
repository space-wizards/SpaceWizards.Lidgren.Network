/* Copyright (c) 2010 Michael Lidgren

Permission is hereby granted, free of charge, to any person obtaining a copy of this software
and associated documentation files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom
the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE
USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Lidgren.Network
{
	/// <summary>
	/// Exception thrown in the Lidgren Network Library
	/// </summary>
	public sealed class NetException : Exception
	{
		/// <summary>
		/// NetException constructor
		/// </summary>
		public NetException()
			: base()
		{
		}

		/// <summary>
		/// NetException constructor
		/// </summary>
		public NetException(string message)
			: base(message)
		{
		}

		/// <summary>
		/// NetException constructor
		/// </summary>
		public NetException(string message, Exception inner)
			: base(message, inner)
		{
		}

		/// <summary>
		/// Throws an exception, in DEBUG only, if first parameter is false
		/// </summary>
		[Conditional("DEBUG")]
		public static void Assert(bool isOk, string message)
		{
			if (!isOk)
				throw new NetException(message);
		}

		/// <summary>
		/// Throws an exception, in DEBUG only, if first parameter is false
		/// </summary>
		[Conditional("DEBUG")]
		public static void Assert(bool isOk)
		{
			if (!isOk)
				throw new NetException();
		}

		/// <summary>Returns the passed <paramref name="argument"/>, but throws an <see cref="NetException"/> if the <paramref name="argument"/> is null.</summary>
		/// <param name="argument">The reference type argument to validate as non-null.</param>
		/// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
#if !DEBUG
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ThrowIfNull<T>(T argument, string? paramName = null)
		{
			return argument;
		}
#else
#if NET5_0_OR_GREATER
		public static T ThrowIfNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null) where T : class
#else
		public static T ThrowIfNull<T>([NotNull] T? argument, string? paramName = null) where T : class
#endif
		{
			return argument ?? throw new NetException($"{paramName ?? "argument"} is null");
		}
	}
#endif
}
