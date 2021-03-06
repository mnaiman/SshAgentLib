﻿//
// PemKeyFormatter.cs
//
// Author(s): David Lechner <david@lechnology.com>
//
// Copyright (c) 2012-2013 David Lechner
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace dlech.SshAgentLib
{
  /// <summary>
  /// Formats SSH private keys in PEM format.
  /// </summary>
  public class PemKeyFormatter : KeyFormatter
  {

    public override void Serialize(Stream aStream, object aObject)
    {
      /* check for required parameters */
      if (aStream == null) {
        throw new ArgumentNullException("aStream");
      }
      if (aObject == null) {
        throw new ArgumentNullException("aObject");
      }
      PasswordFinder pwFinder = null;
      if (GetPassphraseCallbackMethod != null) {
        pwFinder = new PasswordFinder(GetPassphraseCallbackMethod);
      }
      StreamWriter streamWriter = new StreamWriter(aStream);
      PemWriter writer = new PemWriter(streamWriter);
      PinnedArray<char> passphrase = null;
      if (pwFinder != null) {
        passphrase = new PinnedArray<char>(0);
        passphrase.Data = pwFinder.GetPassword();
      }
      if (passphrase == null) {
        writer.WriteObject(aObject);
      } else {
        writer.WriteObject(aObject, null, passphrase.Data, null);
        passphrase.Dispose();
      }
    }

    public override object Deserialize(Stream aStream)
    {
      /* check for required parameters */
      if (aStream == null) {
        throw new ArgumentNullException("aStream");
      }
      PasswordFinder pwFinder = null;
      if (GetPassphraseCallbackMethod != null) {
        pwFinder = new PasswordFinder(GetPassphraseCallbackMethod);
      }
      try {
        StreamReader streamReader = new StreamReader(aStream);
        PemReader reader = new PemReader(streamReader, pwFinder);
        object data = reader.ReadObject();

        if (data is AsymmetricCipherKeyPair) {
          return new SshKey(SshVersion.SSH2, (AsymmetricCipherKeyPair)data);
        } else {
          throw new KeyFormatterException("bad data");
        }
      } catch (PasswordException ex) {
        if (GetPassphraseCallbackMethod == null) {
          throw new CallbackNullException();
        }
        throw new KeyFormatterException("see inner exception", ex);
      } catch (KeyFormatterException) {
        throw;
      } catch (Exception ex) {
        throw new KeyFormatterException("see inner exception", ex);
      }
    }

    private class PasswordFinder : IPasswordFinder
    {
      private GetPassphraseCallback mCallback;

      public PasswordFinder(GetPassphraseCallback aCallback)
      {
        mCallback = aCallback;
      }

      public char[] GetPassword()
      {
        SecureString passphrase = mCallback.Invoke(null);
        char[] passwordChars = new char[passphrase.Length];
        IntPtr passphrasePtr = Marshal.SecureStringToGlobalAllocUnicode(passphrase);
        for (int i = 0; i < passphrase.Length; i++) {
          passwordChars[i] = (char)Marshal.ReadInt16(passphrasePtr, i * 2);
        }
        Marshal.ZeroFreeGlobalAllocUnicode(passphrasePtr);
        return passwordChars;
      }
    }

  }
}
