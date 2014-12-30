﻿/*
 * Copyright (C) 2011 Colin Mackie.
 * This software is distributed under the terms of the GNU General Public License.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using System.Collections;

#if NUNIT
using NUnit.Framework;
#endif

#if NETCF
using OpenNETCF.Security.Cryptography;
#endif

namespace WinAuth
{
	/// <summary>Class that implements base RFC 4226 an RFC 6238 authenticator</summary>
	public abstract class Authenticator : ICloneable
	{
		/// <summary>Number of bytes making up the salt</summary>
		private const int SALT_LENGTH = 8;

		/// <summary>Number of iterations in PBKDF2 key generation</summary>
		private const int PBKDF2_ITERATIONS = 2000;

		/// <summary>Size of derived PBKDF2 key</summary>
		private const int PBKDF2_KEYSIZE = 256;

		/// <summary>Version for encrpytion changes</summary>
		private static string ENCRYPTION_HEADER = ByteArrayToString(Encoding.UTF8.GetBytes("WINAUTH3"));

		//public const decimal DEAFULT_CONFIG_VERSION = (decimal)3.0;
		//private const string BMA_HASH_NAME = "com.blizzard.bma.AUTH_STORE.HASH";
		//private const string BMA_OFFSET_NAME = "com.blizzard.bma.AUTH_STORE.CLOCK_OFFSET";
		/// <summary>Default config version for loading an unknown file</summary>
		/// <summary>Name attribute of the "string" hash element in the Battle.net Mobile Authenticator file</summary>
		/// <summary>Name attribute of the "long" offset element in the Battle.net Mobile Authenticator file</summary>
		/// <summary>Default number of digits in code</summary>
		public const int DEFAULT_CODE_DIGITS = 6;

		//private static byte[] MOBILE_AUTHENTICATOR_KEY = new byte[]
		//  {
		//    0x39,0x8e,0x27,0xfc,0x50,0x27,0x6a,0x65,0x60,0x65,0xb0,0xe5,0x25,0xf4,0xc0,0x6c,
		//    0x04,0xc6,0x10,0x75,0x28,0x6b,0x8e,0x7a,0xed,0xa5,0x9d,0xa9,0x81,0x3b,0x5d,0xd6,
		//    0xc8,0x0d,0x2f,0xb3,0x80,0x68,0x77,0x3f,0xa5,0x9b,0xa4,0x7c,0x17,0xca,0x6c,0x64,
		//    0x79,0x01,0x5c,0x1d,0x5b,0x8b,0x8f,0x6b,0x9a
		//  };
		/// <summary>Private encrpytion key used to encrypt Battle.net mobile authenticator data.</summary>
		/// <summary>Type of password to use to encrypt secret data</summary>
		public enum PasswordTypes
		{
			None = 0,
			Explicit = 1,
			User = 2,
			Machine = 4
		}

		#region Authenticator data

		//public virtual string Serial { get; set; }
		/// <summary>Serial number of authenticator</summary>
		/// <summary>Secret key used for Authenticator</summary>
		public byte[] SecretKey { get; set; }

		/// <summary>Time difference in milliseconds of our machine and server</summary>
		public long ServerTimeDiff { get; set; }

		/// <summary>Time of last synced</summary>
		public long LastServerTime { get; set; }

		/// <summary>Type of password used to encrypt secretdata</summary>
		public PasswordTypes PasswordType { get; private set; }

		/// <summary>Password used to encrypt secretdata (if PasswordType == Explict)</summary>
		protected string Password { get; set; }

		/// <summary>Hash of secret data to detect changes</summary>
		protected byte[] SecretHash { get; private set; }

		public bool RequiresPassword { get; private set; }

		/// <summary>The data current saved with the current encryption and/or password (might be none)</summary>
		protected string EncryptedData { get; private set; }

		/// <summary>Number of digits returned in code (default is 6)</summary>
		public int CodeDigits { get; set; }

		/// <summary>Get/set the combined secret data value</summary>
		public virtual string SecretData
		{
			get
			{
				// this is the secretkey
				//return Authenticator.ByteArrayToString(SecretKey) + "|" + (string.IsNullOrEmpty(Script) == false ? Authenticator.ByteArrayToString(Encoding.UTF8.GetBytes(Script)) : string.Empty);
				return ByteArrayToString(SecretKey);
			}
			set
			{
				if (string.IsNullOrEmpty(value) == false)
				{
					string[] parts = value.Split('|');
					SecretKey = StringToByteArray(parts[0]);
					//Script = (parts.Length > 1 ? Encoding.UTF8.GetString(Authenticator.StringToByteArray(parts[1])) : null);
				}
				else
				{
					SecretKey = null;
					//Script = null;
				}
			}
		}

		//public string Script {get; set;}
		/// <summary>Advanced script saved with authenticator so it is also encrypted</summary>
		/// <summary>Get the server time since 1/1/70</summary>
		public long ServerTime { get { return CurrentTime + ServerTimeDiff; } }

		/// <summary>Calculate the code interval based on the calculated server time</summary>
		public long CodeInterval
		{
			get
			{
				// calculate the code interval; the server's time div 30,000
				return (CurrentTime + ServerTimeDiff)/30000L;
			}
		}

		/// <summary>Get the current code for the authenticator.</summary>
		/// <returns>authenticator code</returns>
		public string CurrentCode
		{
			get
			{
				if (SecretKey == null && EncryptedData != null)
				{
					throw new EncrpytedSecretDataException();
				}

				return CalculateCode(false);
			}
		}

		#endregion

		/// <summary>Static initializer</summary>
		static Authenticator()
		{
			// Issue#71: remove the default .net expect header, which can cause issues (http://stackoverflow.com/questions/566437/)
			ServicePointManager.Expect100Continue = false;
		}

		/// <summary>Create a new Authenticator object</summary>
		public Authenticator()
		{
			CodeDigits = DEFAULT_CODE_DIGITS;
		}

		/// <summary>Create a new Authenticator object</summary>
		public Authenticator(int codeDigits)
		{
			CodeDigits = codeDigits;
		}

		/// <summary>Calculate the current code for the authenticator.</summary>
		/// <param name="resyncTime">flag to resync time</param>
		/// <returns>authenticator code</returns>
		protected virtual string CalculateCode(bool resyncTime)
		{
			// sync time if required
			if (resyncTime == true || ServerTimeDiff == 0)
			{
				Sync();
			}

			HMac hmac = new HMac(new Sha1Digest());
			hmac.Init(new KeyParameter(SecretKey));

			byte[] codeIntervalArray = BitConverter.GetBytes(CodeInterval);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(codeIntervalArray);
			}
			hmac.BlockUpdate(codeIntervalArray, 0, codeIntervalArray.Length);

			byte[] mac = new byte[hmac.GetMacSize()];
			hmac.DoFinal(mac, 0);

			// the last 4 bits of the mac say where the code starts (e.g. if last 4 bit are 1100, we start at byte 12)
			int start = mac[19] & 0x0f;

			// extract those 4 bytes
			byte[] bytes = new byte[4];
			Array.Copy(mac, start, bytes, 0, 4);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(bytes);
			}
			uint fullcode = BitConverter.ToUInt32(bytes, 0) & 0x7fffffff;

			// we use the last 8 digits of this code in radix 10
			uint codemask = (uint) Math.Pow(10, CodeDigits);
			string format = new string('0', CodeDigits);
			string code = (fullcode%codemask).ToString(format);

			return code;
		}

		/// <summary>
		///     Synchorise this authenticator's time with server time. We update our data record with the difference from our UTC
		///     time.
		/// </summary>
		public abstract void Sync();

		#region Load / Save

		public static Authenticator ReadXmlv2(XmlReader reader, string password = null)
		{
			Authenticator authenticator = null;
			string authenticatorType = reader.GetAttribute("type");
			if (string.IsNullOrEmpty(authenticatorType) == false)
			{
				authenticatorType = authenticatorType.Replace("WindowsAuthenticator.", "WinAuth.");
				Type type = Assembly.GetExecutingAssembly().GetType(authenticatorType, false, true);
				authenticator = Activator.CreateInstance(type) as Authenticator;
			}
			if (authenticator == null)
			{
				authenticator = new BattleNetAuthenticator();
			}

			reader.MoveToContent();
			if (reader.IsEmptyElement)
			{
				reader.Read();
				return null;
			}

			reader.Read();
			while (reader.EOF == false)
			{
				if (reader.IsStartElement())
				{
					switch (reader.Name)
					{
						case "servertimediff":
							authenticator.ServerTimeDiff = reader.ReadElementContentAsLong();
							break;

							//case "restorecodeverified":
							//	authenticator.RestoreCodeVerified = reader.ReadElementContentAsBoolean();
							//	break;

						case "secretdata":
							string encrypted = reader.GetAttribute("encrypted");
							string data = reader.ReadElementContentAsString();

							PasswordTypes passwordType = DecodePasswordTypes(encrypted);

							if (passwordType != PasswordTypes.None)
							{
								// this is an old version so there is no hash
								data = DecryptSequence(data, passwordType, password);
							}

							authenticator.PasswordType = PasswordTypes.None;
							authenticator.SecretData = data;

							break;

						default:
							if (authenticator.ReadExtraXml(reader, reader.Name) == false)
							{
								reader.Skip();
							}
							break;
					}
				}
				else
				{
					reader.Read();
					break;
				}
			}

			return authenticator;
		}

		public virtual bool ReadExtraXml(XmlReader reader, string name)
		{
			return false;
		}

		public static PasswordTypes DecodePasswordTypes(string passwordTypes)
		{
			PasswordTypes passwordType = PasswordTypes.None;
			if (string.IsNullOrEmpty(passwordTypes) == true)
			{
				return passwordType;
			}

			char[] types = passwordTypes.ToCharArray();
			for (int i = types.Length - 1; i >= 0; i--)
			{
				char type = types[i];
				switch (type)
				{
					case 'u':
						passwordType |= PasswordTypes.User;
						break;
					case 'm':
						passwordType |= PasswordTypes.Machine;
						break;
					case 'y':
						passwordType |= PasswordTypes.Explicit;
						break;
					default:
						break;
				}
			}

			return passwordType;
		}

		public static string EncodePasswordTypes(PasswordTypes passwordType)
		{
			StringBuilder encryptedTypes = new StringBuilder();
			if ((passwordType & PasswordTypes.Explicit) != 0)
			{
				encryptedTypes.Append("y");
			}
			if ((passwordType & PasswordTypes.User) != 0)
			{
				encryptedTypes.Append("u");
			}
			if ((passwordType & PasswordTypes.Machine) != 0)
			{
				encryptedTypes.Append("m");
			}

			return encryptedTypes.ToString();
		}

		public void SetEncryption(PasswordTypes passwordType, string password = null)
		{
			// check if still encrpyted
			if (RequiresPassword == true)
			{
				// have to decrypt to be able to re-encrypt
				throw new EncrpytedSecretDataException();
			}

			if (passwordType == PasswordTypes.None)
			{
				RequiresPassword = false;
				EncryptedData = null;
				PasswordType = passwordType;
			}
			else
			{
				using (MemoryStream ms = new MemoryStream())
				{
					// get the plain version
					XmlWriterSettings settings = new XmlWriterSettings();
					settings.Indent = true;
					settings.Encoding = Encoding.UTF8;
					using (XmlWriter encryptedwriter = XmlWriter.Create(ms, settings))
					{
						string encrpytedData = EncryptedData;
						PasswordTypes savedpasswordType = PasswordType;
						try
						{
							PasswordType = PasswordTypes.None;
							EncryptedData = null;
							WriteToWriter(encryptedwriter);
						}
						finally
						{
							PasswordType = savedpasswordType;
							EncryptedData = encrpytedData;
						}
					}
					string data = ByteArrayToString(ms.ToArray());

					// update secret hash
					using (SHA1 sha1 = SHA1.Create())
					{
						SecretHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(SecretData));
					}

					// encrypt
					EncryptedData = EncryptSequence(data, passwordType, password);
					PasswordType = passwordType;
					if (PasswordType == PasswordTypes.Explicit)
					{
						SecretData = null;
						RequiresPassword = true;
					}
				}
			}
		}

		public void Protect()
		{
			if (PasswordType != PasswordTypes.None)
			{
				// check if the data has changed
				//if (this.SecretData != null)
				//{
				//	using (SHA1 sha1 = SHA1.Create())
				//	{
				//		byte[] secretHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(this.SecretData));
				//		if (this.SecretHash == null || secretHash.SequenceEqual(this.SecretHash) == false)
				//		{
				//			// we need to encrypt changed secret data
				//			SetEncryption(this.PasswordType, this.Password);
				//		}
				//	}
				//}

				SecretData = null;
				RequiresPassword = true;
				Password = null;
			}
		}

		public bool Unprotect(string password)
		{
			PasswordTypes passwordType = PasswordType;
			if (passwordType == PasswordTypes.None)
			{
				throw new InvalidOperationException("Cannot Unprotect a non-encrypted authenticator");
			}

			// decrypt
			bool changed = false;
			try
			{
				string data = DecryptSequence(EncryptedData, PasswordType, password);
				using (MemoryStream ms = new MemoryStream(StringToByteArray(data)))
				{
					XmlReader reader = XmlReader.Create(ms);
					changed = ReadXml(reader, password) || changed;
				}
				RequiresPassword = false;
				// calculate hash of current secretdata
				using (SHA1 sha1 = SHA1.Create())
				{
					SecretHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(SecretData));
				}
				// keep the password until we reprotect in case data changes
				Password = password;

				if (changed == true)
				{
					// we need to encrypt changed secret data
					using (MemoryStream ms = new MemoryStream())
					{
						// get the plain version
						XmlWriterSettings settings = new XmlWriterSettings();
						settings.Indent = true;
						settings.Encoding = Encoding.UTF8;
						using (XmlWriter encryptedwriter = XmlWriter.Create(ms, settings))
						{
							WriteToWriter(encryptedwriter);
						}
						string encrypteddata = ByteArrayToString(ms.ToArray());

						// update secret hash
						using (SHA1 sha1 = SHA1.Create())
						{
							SecretHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(SecretData));
						}

						// encrypt
						EncryptedData = EncryptSequence(encrypteddata, passwordType, password);
					}
				}

				return changed;
			}
			catch (EncrpytedSecretDataException)
			{
				RequiresPassword = true;
				throw;
			}
			finally
			{
				PasswordType = passwordType;
			}
		}

		public bool ReadXml(XmlReader reader, string password = null)
		{
			// decode the password type
			string encrypted = reader.GetAttribute("encrypted");
			PasswordTypes passwordType = DecodePasswordTypes(encrypted);
			PasswordType = passwordType;

			if (passwordType != PasswordTypes.None)
			{
				// read the encrypted text from the node
				EncryptedData = reader.ReadElementContentAsString();
				return Unprotect(password);

				//// decrypt
				//try
				//{
				//	string data = Authenticator.DecryptSequence(this.EncryptedData, passwordType, password);
				//	using (MemoryStream ms = new MemoryStream(Authenticator.StringToByteArray(data)))
				//	{
				//		reader = XmlReader.Create(ms);
				//		this.ReadXml(reader, password);
				//	}
				//}
				//catch (EncrpytedSecretDataException)
				//{
				//	this.RequiresPassword = true;
				//	throw;
				//}
				//finally
				//{
				//	this.PasswordType = passwordType;
				//}
			}

			reader.MoveToContent();
			if (reader.IsEmptyElement)
			{
				reader.Read();
				return false;
			}

			reader.Read();
			while (reader.EOF == false)
			{
				if (reader.IsStartElement())
				{
					switch (reader.Name)
					{
						case "lastservertime":
							LastServerTime = reader.ReadElementContentAsLong();
							break;

						case "servertimediff":
							ServerTimeDiff = reader.ReadElementContentAsLong();
							break;

						case "secretdata":
							SecretData = reader.ReadElementContentAsString();
							break;

						default:
							if (ReadExtraXml(reader, reader.Name) == false)
							{
								reader.Skip();
							}
							break;
					}
				}
				else
				{
					reader.Read();
					break;
				}
			}

			// check if we need to sync, or if it's been a day
			if (ServerTimeDiff == 0 || LastServerTime == 0 || LastServerTime < DateTime.Now.AddHours(-24).Ticks)
			{
				Sync();
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>Write this authenticator into an XmlWriter</summary>
		/// <param name="writer">XmlWriter to receive authenticator</param>
		public void WriteToWriter(XmlWriter writer)
		{
			writer.WriteStartElement("authenticatordata");
			//writer.WriteAttributeString("type", this.GetType().FullName);
			string encrypted = EncodePasswordTypes(PasswordType);
			if (string.IsNullOrEmpty(encrypted) == false)
			{
				writer.WriteAttributeString("encrypted", encrypted);
			}

			if (PasswordType != PasswordTypes.None)
			{
				writer.WriteRaw(EncryptedData);
			}
			else
			{
				writer.WriteStartElement("servertimediff");
				writer.WriteString(ServerTimeDiff.ToString());
				writer.WriteEndElement();
				//
				writer.WriteStartElement("lastservertime");
				writer.WriteString(LastServerTime.ToString());
				writer.WriteEndElement();
				//
				writer.WriteStartElement("secretdata");
				writer.WriteString(SecretData);
				writer.WriteEndElement();

				WriteExtraXml(writer);
			}

			/*
						if (passwordType != Authenticator.PasswordTypes.None)
						{
							//string data = this.EncryptedData;
							//if (data == null)
							//{
							//	using (MemoryStream ms = new MemoryStream())
							//	{
							//		XmlWriterSettings settings = new XmlWriterSettings();
							//		settings.Indent = true;
							//		settings.Encoding = Encoding.UTF8;
							//		using (XmlWriter encryptedwriter = XmlWriter.Create(ms, settings))
							//		{
							//			Authenticator.PasswordTypes savedpasswordType = PasswordType;
							//			PasswordType = Authenticator.PasswordTypes.None;
							//			WriteToWriter(encryptedwriter);
							//			PasswordType = savedpasswordType;
							//		}
							//		data = Authenticator.ByteArrayToString(ms.ToArray());
							//	}

							//	data = Authenticator.EncryptSequence(data, PasswordType, Password);
							//}

							writer.WriteString(this.EncryptedData);
							writer.WriteEndElement();

							return;
						}

						//
						writer.WriteStartElement("servertimediff");
						writer.WriteString(ServerTimeDiff.ToString());
						writer.WriteEndElement();
						//
						writer.WriteStartElement("secretdata");
				  writer.WriteString(SecretData);
				  writer.WriteEndElement();

						WriteExtraXml(writer);
			*/

			writer.WriteEndElement();
		}

		/*
				/// <summary>
				/// Write this authenticator into an XmlWriter
				/// </summary>
				/// <param name="writer">XmlWriter to receive authenticator</param>
				protected void WriteToWriter(XmlWriter writer, PasswordTypes passwordType)
				{
					if (passwordType != Authenticator.PasswordTypes.None)
					{
						writer.WriteStartElement("authenticatordata");
						writer.WriteAttributeString("encrypted", EncodePasswordTypes(this.PasswordType));
						writer.WriteString(this.EncryptedData);
						writer.WriteEndElement();
					}
					else
					{
						writer.WriteStartElement("servertimediff");
						writer.WriteString(ServerTimeDiff.ToString());
						writer.WriteEndElement();
						//
						writer.WriteStartElement("secretdata");
						writer.WriteString(SecretData);
						writer.WriteEndElement();

						WriteExtraXml(writer);
					}
				}
		*/

		/// <summary>Virtual function to write any class specific xml nodes into the writer</summary>
		/// <param name="writer">XmlWriter to write data</param>
		protected virtual void WriteExtraXml(XmlWriter writer) {}

		#endregion

		#region Utility functions

		/// <summary>
		///     Create a one-time pad by generating a random block and then taking a hash of that block as many times as
		///     needed.
		/// </summary>
		/// <param name="length">desired pad length</param>
		/// <returns>array of bytes conatining random data</returns>
		protected internal static byte[] CreateOneTimePad(int length)
		{
			// There is a MITM vulnerability from using the standard Random call
			// see https://docs.google.com/document/edit?id=1pf-YCgUnxR4duE8tr-xulE3rJ1Hw-Bm5aMk5tNOGU3E&hl=en
			// in http://code.google.com/p/winauth/issues/detail?id=2
			// so we switch out to use RNGCryptoServiceProvider instead of Random

			RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();

			byte[] randomblock = new byte[length];

			SHA1 sha1 = SHA1.Create();
			int i = 0;
			do
			{
				byte[] hashBlock = new byte[128];
				random.GetBytes(hashBlock);

				byte[] key = sha1.ComputeHash(hashBlock, 0, hashBlock.Length);
				if (key.Length >= randomblock.Length)
				{
					Array.Copy(key, 0, randomblock, i, randomblock.Length);
					break;
				}
				Array.Copy(key, 0, randomblock, i, key.Length);
				i += key.Length;
			} while (true);

			return randomblock;
		}

		/// <summary>Get the milliseconds since 1/1/70 (same as Java currentTimeMillis)</summary>
		public static long CurrentTime
		{
			get { return Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds); }
		}

		/// <summary>Convert a hex string into a byte array. E.g. "001f406a" -> byte[] {0x00, 0x1f, 0x40, 0x6a}</summary>
		/// <param name="hex">hex string to convert</param>
		/// <returns>byte[] of hex string</returns>
		public static byte[] StringToByteArray(string hex)
		{
			int len = hex.Length;
			byte[] bytes = new byte[len/2];
			for (int i = 0; i < len; i += 2)
			{
				bytes[i/2] = Convert.ToByte(hex.Substring(i, 2), 16);
			}
			return bytes;
		}

		/// <summary>Convert a byte array into a ascii hex string, e.g. byte[]{0x00,0x1f,0x40,ox6a} -> "001f406a"</summary>
		/// <param name="bytes">byte array to convert</param>
		/// <returns>string version of byte array</returns>
		public static string ByteArrayToString(byte[] bytes)
		{
			// Use BitConverter, but it sticks dashes in the string
			return BitConverter.ToString(bytes).Replace("-", string.Empty);
		}

		public static string DecryptSequence(string data, PasswordTypes encryptedTypes, string password, bool decode = false)
		{
			// check for encrpytion header
			if (data.Length < ENCRYPTION_HEADER.Length || data.IndexOf(ENCRYPTION_HEADER) != 0)
			{
				return DecryptSequenceNoHash(data, encryptedTypes, password, decode);
			}

			// extract salt and hash
			using (var sha = new SHA256Managed())
			{
				// jump header
				int datastart = ENCRYPTION_HEADER.Length;
				string salt = data.Substring(datastart, Math.Min(SALT_LENGTH*2, data.Length - datastart));
				datastart += salt.Length;
				string hash = data.Substring(datastart, Math.Min(sha.HashSize/8*2, data.Length - datastart));
				datastart += hash.Length;
				data = data.Substring(datastart);

				// decrypt encrypt content and try again
				//char[] encTypes = encryptedTypes.ToCharArray();

				data = DecryptSequenceNoHash(data, encryptedTypes, password);

				// check the hash
				byte[] compareplain = StringToByteArray(salt + data);
				string comparehash = ByteArrayToString(sha.ComputeHash(compareplain));
				if (string.Compare(comparehash, hash) != 0)
				{
					throw new BadPasswordException();
				}
			}

			return data;
		}

		//public static void Log(string message)
		//{
		//	try
		//	{
		//		string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		//		File.AppendAllText(Path.Combine(dir, "winauth.log"), message);
		//	}
		//	catch (Exception) { }
		//}

		private static string DecryptSequenceNoHash(
			string data,
			PasswordTypes encryptedTypes,
			string password,
			bool decode = false)
		{
			try
			{
				// reverse order they were encrypted
				if ((encryptedTypes & PasswordTypes.Machine) != 0)
				{
					// we are going to decrypt with the Windows local machine key
					byte[] cipher = StringToByteArray(data);
					//Log("Decrypt Machine: " + data + Environment.NewLine);
					byte[] plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.LocalMachine);
					if (decode == true)
					{
						//Log("Decode Machine: " + ByteArrayToString(plain) + Environment.NewLine);
						data = Encoding.UTF8.GetString(plain, 0, plain.Length);
					}
					else
					{
						data = ByteArrayToString(plain);
					}
					//Log("Decrypted Machine: " + data + Environment.NewLine);
				}
				if ((encryptedTypes & PasswordTypes.User) != 0)
				{
					// we are going to decrypt with the Windows User account key
					byte[] cipher = StringToByteArray(data);
					//Log("Decrypt User: " + data + Environment.NewLine);
					byte[] plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
					if (decode == true)
					{
						//Log("Decode User: " + ByteArrayToString(plain) + Environment.NewLine);
						data = Encoding.UTF8.GetString(plain, 0, plain.Length);
					}
					else
					{
						data = ByteArrayToString(plain);
					}
					//Log("Decrypted User: " + data + Environment.NewLine);
				}
				if ((encryptedTypes & PasswordTypes.Explicit) != 0)
				{
					// we use an explicit password to encrypt data
					if (string.IsNullOrEmpty(password) == true)
					{
						throw new EncrpytedSecretDataException();
					}
					///Log("Decrypt Explicit: " + password + " " + data + Environment.NewLine);
					data = Decrypt(data, password, true);
					//Log("Decrypted Explicit: " + data + Environment.NewLine);
					if (decode == true)
					{
						byte[] plain = StringToByteArray(data);
						data = Encoding.UTF8.GetString(plain, 0, plain.Length);
						//Log("Decode Explicit: " + data + Environment.NewLine);
					}
				}
			}
			catch (EncrpytedSecretDataException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new BadPasswordException(ex.Message, ex);
			}

			return data;
		}

		public static string EncryptSequence(string data, PasswordTypes passwordType, string password)
		{
			// get hash of original
			string salt;
			using (var random = new RNGCryptoServiceProvider())
			{
				byte[] saltbytes = new byte[SALT_LENGTH];
				random.GetBytes(saltbytes);
				salt = ByteArrayToString(saltbytes);
			}
			string hash;
			using (var sha = new SHA256Managed())
			{
				byte[] plain = StringToByteArray(salt + data);
				hash = ByteArrayToString(sha.ComputeHash(plain));
			}

			if ((passwordType & PasswordTypes.Explicit) != 0)
			{
				string encrypted = Encrypt(data, password);

				// test the encryption
				string decrypted = Decrypt(encrypted, password, true);
				if (string.Compare(data, decrypted) != 0)
				{
					throw new InvalidEncryptionException(data, password, encrypted, decrypted);
				}
				data = encrypted;
			}
			if ((passwordType & PasswordTypes.User) != 0)
			{
				// we encrypt the data using the Windows User account key
				byte[] plain = StringToByteArray(data);
				byte[] cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
				data = ByteArrayToString(cipher);
			}
			if ((passwordType & PasswordTypes.Machine) != 0)
			{
				// we encrypt the data using the Local Machine account key
				byte[] plain = StringToByteArray(data);
				byte[] cipher = ProtectedData.Protect(plain, null, DataProtectionScope.LocalMachine);
				data = ByteArrayToString(cipher);
			}

			// prepend the salt + hash
			return ENCRYPTION_HEADER + salt + hash + data;
		}

		/// <summary>Encrypt a string with a given key</summary>
		/// <param name="plain">data to encrypt - hex representation of byte array</param>
		/// <param name="key">key to use to encrypt</param>
		/// <returns>hex coded encrypted string</returns>
		public static string Encrypt(string plain, string password)
		{
			byte[] inBytes = StringToByteArray(plain);
			byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

			// build a new salt
			RNGCryptoServiceProvider rg = new RNGCryptoServiceProvider();
			byte[] saltbytes = new byte[SALT_LENGTH];
			rg.GetBytes(saltbytes);
			string salt = ByteArrayToString(saltbytes);

			// build our PBKDF2 key
#if NETCF
			PBKDF2 kg = new PBKDF2(passwordBytes, saltbytes, PBKDF2_ITERATIONS);
#else
			Rfc2898DeriveBytes kg = new Rfc2898DeriveBytes(passwordBytes, saltbytes, PBKDF2_ITERATIONS);
#endif
			byte[] key = kg.GetBytes(PBKDF2_KEYSIZE);

			// get our cipher
			BufferedBlockCipher cipher = new PaddedBufferedBlockCipher(new BlowfishEngine(), new ISO10126d2Padding());
			cipher.Init(true, new KeyParameter(key));

			// encrypt data
			int osize = cipher.GetOutputSize(inBytes.Length);
			byte[] outBytes = new byte[osize];
			int olen = cipher.ProcessBytes(inBytes, 0, inBytes.Length, outBytes, 0);
			olen += cipher.DoFinal(outBytes, olen);
			if (olen < osize)
			{
				byte[] t = new byte[olen];
				Array.Copy(outBytes, 0, t, 0, olen);
				outBytes = t;
			}

			// return encoded byte->hex string
			return salt + ByteArrayToString(outBytes);
		}

		/// <summary>Decrypt a hex-coded string using our MD5 or PBKDF2 generated key</summary>
		/// <param name="data">data string to be decrypted</param>
		/// <param name="key">decryption key</param>
		/// <param name="PBKDF2">flag to indicate we are using PBKDF2 to generate derived key</param>
		/// <returns>hex coded decrypted string</returns>
		public static string Decrypt(string data, string password, bool PBKDF2)
		{
			byte[] key;
			byte[] saltBytes = StringToByteArray(data.Substring(0, SALT_LENGTH*2));

			if (PBKDF2 == true)
			{
				// extract the salt from the data
				byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

				// build our PBKDF2 key
#if NETCF
			PBKDF2 kg = new PBKDF2(passwordBytes, saltbytes, 2000);
#else
				Rfc2898DeriveBytes kg = new Rfc2898DeriveBytes(passwordBytes, saltBytes, PBKDF2_ITERATIONS);
#endif
				key = kg.GetBytes(PBKDF2_KEYSIZE);
			}
			else
			{
				// extract the salt from the data
				byte[] passwordBytes = Encoding.Default.GetBytes(password);
				key = new byte[saltBytes.Length + passwordBytes.Length];
				Array.Copy(saltBytes, key, saltBytes.Length);
				Array.Copy(passwordBytes, 0, key, saltBytes.Length, passwordBytes.Length);
				// build out combined key
				MD5 md5 = MD5.Create();
				key = md5.ComputeHash(key);
			}

			// extract the actual data to be decrypted
			byte[] inBytes = StringToByteArray(data.Substring(SALT_LENGTH*2));

			// get cipher
			BufferedBlockCipher cipher = new PaddedBufferedBlockCipher(new BlowfishEngine(), new ISO10126d2Padding());
			cipher.Init(false, new KeyParameter(key));

			// decrypt the data
			int osize = cipher.GetOutputSize(inBytes.Length);
			byte[] outBytes = new byte[osize];
			try
			{
				int olen = cipher.ProcessBytes(inBytes, 0, inBytes.Length, outBytes, 0);
				olen += cipher.DoFinal(outBytes, olen);
				if (olen < osize)
				{
					byte[] t = new byte[olen];
					Array.Copy(outBytes, 0, t, 0, olen);
					outBytes = t;
				}
			}
			catch (Exception)
			{
				// an exception is due to bad password
				throw new BadPasswordException();
			}

			// return encoded string
			return ByteArrayToString(outBytes);
		}

		/// <summary>Wrapped TryParse for compatability with NETCF35 to simulate long.TryParse</summary>
		/// <param name="s">string of value to parse</param>
		/// <param name="val">out long value</param>
		/// <returns>true if value was parsed</returns>
		protected internal static bool LongTryParse(string s, out long val)
		{
#if NETCF
			try
			{
				val = long.Parse(s);
				return true;
			}
			catch (Exception )
			{
				val = 0;
				return false;
			}
#else
			return long.TryParse(s, out val);
#endif
		}

		#endregion

		#region ICloneable

		/// <summary>Clone the current object</summary>
		/// <returns>return clone</returns>
		public object Clone()
		{
			// we only need to do shallow copy
			return MemberwiseClone();
		}

		#endregion

#if NETCF
	/// <summary>
	/// Private class that implements PBKDF2 needed for older NETCF. Implemented from http://en.wikipedia.org/wiki/PBKDF2.
	/// </summary>
		private class PBKDF2
		{
			/// <summary>
			/// Our digest
			/// </summary>
			private HMac m_mac;

			/// <summary>
			/// Digest length
			/// </summary>
			private int m_hlen;

			/// <summary>
			/// Base password
			/// </summary>
			private byte[] m_password;

			/// <summary>
			/// Salt
			/// </summary>
			private byte[] m_salt;

			/// <summary>
			/// Number of iterations
			/// </summary>
			private int m_iterations;

			/// <summary>
			/// Create a new PBKDF2 object
			/// </summary>
			public PBKDF2(byte[] password, byte[] salt, int iterations)
			{
				m_password = password;
				m_salt = salt;
				m_iterations = iterations;

				m_mac = new HMac(new Sha1Digest());
				m_hlen = m_mac.GetMacSize();
			}

			/// <summary>
			/// Calculate F.
			/// F(P,S,c,i) = U1 ^ U2 ^ ... ^ Uc
			/// Where F is an xor of c iterations of chained PRF. First iteration of PRF uses master password P as PRF key and salt concatenated to i. Second and greater PRF uses P and output of previous PRF computation:
			/// </summary>
			/// <param name="P"></param>
			/// <param name="S"></param>
			/// <param name="c"></param>
			/// <param name="i"></param>
			/// <param name="DK"></param>
			/// <param name="DKoffset"></param>
			private void F(byte[] P, byte[] S, int c, byte[] i, byte[] DK, int DKoffset)
			{
				// first iteration (ses master password P as PRF key and salt concatenated to i)
				byte[] buf = new byte[m_hlen];
				ICipherParameters param = new KeyParameter(P);
				m_mac.Init(param);
				m_mac.BlockUpdate(S, 0, S.Length);
				m_mac.BlockUpdate(i, 0, i.Length);
				m_mac.DoFinal(buf, 0);
				Array.Copy(buf, 0, DK, DKoffset, buf.Length);

				// remaining iterations (uses P and output of previous PRF computation)
				for (int iter = 1; iter < c; iter++)
				{
					m_mac.Init(param);
					m_mac.BlockUpdate(buf, 0, buf.Length);
					m_mac.DoFinal(buf, 0);

					for (int j=buf.Length-1; j >= 0; j--)
					{
						DK[DKoffset + j] ^= buf[j];
					}
				}
			}

			/// <summary>
			/// Calculate a derived key of dkLen bytes long from our initial password and salt
			/// </summary>
			/// <param name="dkLen">Length of desired key to be returned</param>
			/// <returns>derived key of dkLen bytes</returns>
			public byte[] GetBytes(int dkLen)
			{
				// For each hLen-bit block Ti of derived key DK, computing is as follows:
				//  DK = T1 || T2 || ... || Tdklen/hlen
				//  Ti = F(P,S,c,i)
				int chunks = (dkLen + m_hlen - 1) / m_hlen;
				byte[] DK = new byte[chunks * m_hlen];
				byte[] idata = new byte[4];
				for (int i = 1; i <= chunks; i++)
				{
					idata[0] = (byte)((uint)i >> 24);
					idata[1] = (byte)((uint)i >> 16);
					idata[2] = (byte)((uint)i >> 8);
					idata[3] = (byte)i; 

					F(m_password, m_salt, m_iterations, idata, DK, (i-1) * m_hlen);
				}
				if (DK.Length > dkLen)
				{
					byte[] reduced = new byte[dkLen];
					Array.Copy(DK, 0, reduced, 0, dkLen);
					DK = reduced;
				}

				return DK;
			}
		}

#if NUNIT
		/// Test against standard test vectors and one of our own of the correct iterations.
		/// http://tools.ietf.org/html/draft-josefsson-pbkdf2-test-vectors-00
		[Test]
		public static void TestPBKDF2()
		{
			PBKDF2 kg;
			byte[] DK;

			byte[] tv1 = new byte[] { 0x0c, 0x60, 0xc8, 0x0f, 0x96, 0x1f, 0x0e, 0x71, 0xf3, 0xa9, 0xb5, 0x24, 0xaf, 0x60, 0x12, 0x06, 0x2f, 0xe0, 0x37, 0xa6 };
			kg = new PBKDF2(Encoding.Default.GetBytes("password"), Encoding.Default.GetBytes("salt"), 1);
			DK = kg.GetBytes(20);
			Assert.AreEqual(DK, tv1);

			byte[] tv2 = new byte[] { 0xea, 0x6c, 0x01, 0x4d, 0xc7, 0x2d, 0x6f, 0x8c, 0xcd, 0x1e, 0xd9, 0x2a, 0xce, 0x1d, 0x41, 0xf0, 0xd8, 0xde, 0x89, 0x57 };
			kg = new PBKDF2(Encoding.Default.GetBytes("password"), Encoding.Default.GetBytes("salt"), 2);
			DK = kg.GetBytes(20);
			Assert.AreEqual(DK, tv2);

			byte[] tv3 = new byte[] { 0x4b, 0x00, 0x79, 0x01, 0xb7, 0x65, 0x48, 0x9a, 0xbe, 0xad, 0x49, 0xd9, 0x26, 0xf7, 0x21, 0xd0, 0x65, 0xa4, 0x29, 0xc1 };
			kg = new PBKDF2(Encoding.Default.GetBytes("password"), Encoding.Default.GetBytes("salt"), 4096);
			DK = kg.GetBytes(20);
			Assert.AreEqual(DK, tv3);

			byte[] tv4 = new byte[] { 0x2f, 0x25, 0x5b, 0x3a, 0x95, 0x46, 0x3c, 0x76, 0x62, 0x1f, 0x06, 0x80, 0xa2, 0xb3, 0x35, 0xad, 0x90, 0x3b, 0x85, 0xde };
			kg = new PBKDF2(Encoding.Default.GetBytes("VXFr[24c=6(D8He"), Encoding.Default.GetBytes("salt"), 1000);
			DK = kg.GetBytes(20);
			Assert.AreEqual(DK, tv4);
		}
#endif

#endif

	}
}