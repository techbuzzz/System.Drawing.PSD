﻿using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace System.Drawing.PSD
{
	public partial class Layer
	{
		public class Channel
		{
			/// <summary>
			/// The layer to which this channel belongs
			/// </summary>
			public Layer Layer { get; private set; }


			/// <summary>
			/// 0 = red, 1 = green, etc.
			/// 1 = transparency mask
			/// 2 = user supplied layer mask
			/// </summary>
			public Int16 ID { get; set; }

			/// <summary>
			/// The length of the compressed channel data.
			/// </summary>
			public Int32 Length;

			/// <summary>
			/// The compressed raw channel data
			/// </summary>
			public Byte[] Data { get; set; }
			public Byte[] ImageData { get; set; }
			public ImageCompression ImageCompression { get; set; }

			internal Channel(Int16 id, Layer layer)
			{
				ID = id;
				Layer = layer;
				Layer.Channels.Add(this);
				Layer.SortedChannels.Add(ID, this);
			}

			internal Channel(BinaryReverseReader reverseReader, Layer layer)
			{
				Debug.WriteLine("Channel started at " + reverseReader.BaseStream.Position.ToString(CultureInfo.InvariantCulture));

				ID = reverseReader.ReadInt16();
				Length = reverseReader.ReadInt32();

				Layer = layer;
			}

			internal void Save(BinaryReverseWriter reverseWriter)
			{
				Debug.WriteLine("Channel Save started at " + reverseWriter.BaseStream.Position.ToString(CultureInfo.InvariantCulture));

				reverseWriter.Write(ID);

				CompressImageData();

				reverseWriter.Write(Data.Length + 2); // 2 bytes for the image compression
			}

			internal void LoadPixelData(BinaryReverseReader reverseReader)
			{
				Debug.WriteLine("Channel.LoadPixelData started at " + reverseReader.BaseStream.Position.ToString(CultureInfo.InvariantCulture));

				Data = reverseReader.ReadBytes(Length);

				using (BinaryReverseReader imageReader = DataReader)
				{
					ImageCompression = (ImageCompression)imageReader.ReadInt16();

					Int32 bytesPerRow = 0;

					switch (Layer.PsdFile.Depth)
					{
						case 1:
							bytesPerRow = Layer.Rect.Width;//NOT sure
							break;
						case 8:
							bytesPerRow = Layer.Rect.Width;
							break;
						case 16:
							bytesPerRow = Layer.Rect.Width * 2;
							break;
					}

					ImageData = new Byte[Layer.Rect.Height * bytesPerRow];

					switch (ImageCompression)
					{
						case ImageCompression.Raw:
							imageReader.Read(ImageData, 0, ImageData.Length);
							break;
						case ImageCompression.Rle:
							{
								Int32[] rowLengthList = new Int32[Layer.Rect.Height];

								for (Int32 i = 0; i < rowLengthList.Length; i++) rowLengthList[i] = imageReader.ReadInt16();

								for (Int32 i = 0; i < Layer.Rect.Height; i++)
								{
									Int32 rowIndex = i * Layer.Rect.Width;
									RleHelper.DecodedRow(imageReader.BaseStream, ImageData, rowIndex, bytesPerRow);

									//if (rowLenghtList[i] % 2 == 1)
									//  readerImg.ReadByte();
								}
							}
							break;
					}
				}
			}

			private void CompressImageData()
			{
				if (ImageCompression == ImageCompression.Rle)
				{
					MemoryStream memoryStream = new MemoryStream();
					BinaryReverseWriter reverseWriter = new BinaryReverseWriter(memoryStream);

					// we will write the correct lengths later, so remember 
					// the position
					Int64 lengthPosition = reverseWriter.BaseStream.Position;

					Int32[] rleRowLenghs = new Int32[Layer.Rect.Height];

					if (ImageCompression == ImageCompression.Rle)
					{
						for (Int32 i = 0; i < rleRowLenghs.Length; i++)
						{
							reverseWriter.Write((Int16)0x1234);
						}
					}

					Int32 bytesPerRow = 0;

					switch (Layer.PsdFile.Depth)
					{
						case 1:
							bytesPerRow = Layer.Rect.Width;//NOT Shure
							break;
						case 8:
							bytesPerRow = Layer.Rect.Width;
							break;
						case 16:
							bytesPerRow = Layer.Rect.Width * 2;
							break;
					}

					for (Int32 row = 0; row < Layer.Rect.Height; row++)
					{
						Int32 rowIndex = row * Layer.Rect.Width;
						rleRowLenghs[row] = RleHelper.EncodedRow(reverseWriter.BaseStream, ImageData, rowIndex, bytesPerRow);
					}

					Int64 endPosition = reverseWriter.BaseStream.Position;

					reverseWriter.BaseStream.Position = lengthPosition;

					foreach (Int32 length in rleRowLenghs)
					{
						reverseWriter.Write((Int16)length);
					}

					reverseWriter.BaseStream.Position = endPosition;

					memoryStream.Close();

					Data = memoryStream.ToArray();

					memoryStream.Dispose();

				}
				else
				{
					Data = (byte[])ImageData.Clone();
				}
			}

			internal void SavePixelData(BinaryReverseWriter writer)
			{
				Debug.WriteLine("Channel SavePixelData started at " + writer.BaseStream.Position.ToString(CultureInfo.InvariantCulture));

				writer.Write((short)ImageCompression);
				writer.Write(ImageData);
			}

			public BinaryReverseReader DataReader
			{
				get
				{
					return Data == null ? null : new BinaryReverseReader(new MemoryStream(Data));
				}
			}
		}

	}
}
