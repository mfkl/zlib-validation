using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using System.IO.Compression;
using Elskom.Generic.Libs;
using Joveler.ZLibWrapper;

public static class Program
{
	private static IEnumerable<(Stream, bool? shouldBeLegal)> Truncate(ReadOnlyMemory<byte> source)
	{
		var data = source.ToArray();
		for (int i = 0; i < data.Length; i++)
		{
			yield return (
				new MemoryStream(data, 0, i, writable: false),
				CanByteBeManipulatedSafely(data.Length, i, truncated: true));
		}
	}

	private static IEnumerable<(Stream, bool? shouldBeLegal)> Increment(ReadOnlyMemory<byte> source)
	{
		for (int i = 0; i < source.Length; i++)
		{
			var data = source.ToArray();
			data[i]++;
			yield return (
				new MemoryStream(data, 0, data.Length, writable: false),
				// this will be slightly wrong for some extra headers fields, but it's good enough
				// manipulating any content like this should fail the CRC32
				CanByteBeManipulatedSafely(data.Length, i, truncated: false));
		}
	}

	private static IEnumerable<(Stream, bool? shouldBeLegal)> Unchanged(ReadOnlyMemory<byte> source)
	{
		var data = source.ToArray();
		yield return (
				new MemoryStream(data, 0, data.Length, writable: false),
				true);
	}

	private static bool? CanByteBeManipulatedSafely(int length, int index, bool truncated)
	{
		// https://tools.ietf.org/html/rfc1952.html#section-2.2
		switch (index)
		{
			case 0: // ID1
			case 1: // ID2
			case 2: // CM (always gzip)
				return false;
			//FLG some flags values may be illegal but ignoring for now
			case 3:
				return truncated ? false : (bool?)null;
			// MTIME
			case 4:
			case 5:
			case 6:
			case 7:
				return truncated ? false : true;
			//XFL some flags values may be illegal but ignoring for now
			case 8:
				return truncated ? false : (bool?)null;
			// OS
			case 9:
				return truncated ? false : (bool?)null;
			default:
				// footer
				if (length > 18 && index > length - 4)
				{
					// isize - should be validated
					return false;
				}
				else if (length > 18 && index > length - 8)
				{
					// CRC32 - should be validated - technically certain manipulations could be undetected
					return false;
				}
				// depending on flags/xflags there may be header entries in here that are mutable
				// however if FLG.FHCRC was set thyen they aren't, so just assum none can be changed
				return false;
		}
	}

	private static void RoundTrip(
		ReadOnlySpan<byte> source,
		(string name, Func<Stream, Stream> compress, Func<Stream, Stream> decompress) codec,
		Func<ReadOnlyMemory<byte>, IEnumerable<(Stream input, bool? shouldBeOkay)>> peturbations)
	{
		byte[] data;
		using (var compressed = new MemoryStream())
		using (var gzip = codec.compress(compressed))
		{
			foreach (var b in source)
			{
				gzip.WriteByte(b);
			}
			gzip.Dispose();
			data = compressed.ToArray();
		}

		var expected = new StringBuilder("Expected: ");
		var actual = new StringBuilder("Actual  : ");
		foreach (var entry in peturbations(data))
		{
			if (entry.shouldBeOkay.HasValue)
			{
				expected.Append(entry.shouldBeOkay == true ? "/" : "x");
			}
			else
			{
				expected.Append("?");
			}
			try
			{
				using (var input = entry.input)
				using (var gzip = codec.decompress(input))
				using (var roundtrip = new MemoryStream())
				{
					gzip.CopyTo(roundtrip);
					var result = roundtrip.ToArray();
				}
				actual.Append("/");
			}
			catch (Exception e)
			{
				actual.Append("x");
			}
		}
		Console.WriteLine(expected.ToString());
		Console.WriteLine(actual.ToString());
	}

	public static void Main()
	{
		string dllPath = Path.Combine(@$"{Directory.GetCurrentDirectory()}\Precompiled\x64", "zlibwapi.dll");
		ZLibInit.GlobalInit(dllPath);

		var codecs = new (string name, Func<Stream, Stream> compress, Func<Stream, Stream> decompress)[]
		{
            (
                "System.IO.Compression",
                s => new System.IO.Compression.GZipStream(s, System.IO.Compression.CompressionLevel.Fastest),
                s => new System.IO.Compression.GZipStream(s, CompressionMode.Decompress)
            ),
            (
                "zlib.managed",
                s => new ZOutputStream(s, ZlibCompression.ZBESTSPEED),
                s => new ZInputStream(s)
            ),
			(
				"ICSharpCode.SharpZipLib",
				s =>
				{
					var result = new ICSharpCode.SharpZipLib.GZip.GZipOutputStream(s);
					result.SetLevel(1);
					return result;
				},
				s => new ICSharpCode.SharpZipLib.GZip.GZipInputStream(s)
			),
			(
				"SharpCompress",
				s => new SharpCompress.Compressors.Deflate.GZipStream(s, SharpCompress.Compressors.CompressionMode.Compress),
				s => new SharpCompress.Compressors.Deflate.GZipStream(s, SharpCompress.Compressors.CompressionMode.Decompress)
			),
			(
				"ZLibWrapper ",
				s => new Joveler.ZLibWrapper.DeflateStream(s, Joveler.ZLibWrapper.ZLibMode.Compress),
				s => new Joveler.ZLibWrapper.DeflateStream(s, Joveler.ZLibWrapper.ZLibMode.Decompress)
			)
		};

		var source = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

		foreach (var codec in codecs)
		{
            Console.WriteLine(codec.name);

            Console.WriteLine("Truncate");
			RoundTrip(source, codec, Truncate);

            Console.WriteLine("");
        }

		Console.ReadKey();
	}
}