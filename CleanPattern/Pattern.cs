// originally made by caytchen https://github.com/stschake/cleanCore/tree/master/cleanPattern
// modified by HighVoltz to work out of process

/*
#######################    Simplified BSD License    ########################
# Redistribution and use in source and binary forms, with or without 
# modification, are permitted provided that the following conditions are met:
#    * Redistributions of source code must retain the above copyright 
#      notice, this list of conditions and the following disclaimer.
#    * Redistributions in binary form must reproduce the above copyright 
#      notice, this list of conditions and the following disclaimer in the
#      documentation and/or other materials provided with the 
#      distribution.
# THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS 
# IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
# TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
# PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
# HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
# SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED 
# TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR 
# PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF 
# LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
# NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS 
# SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using GreyMagic;


namespace HighVoltz.HBRelog.CleanPattern
{
    public class Pattern
    {
        public string Name { get; private set; }
        public byte[] Bytes { get; private set; }
        public bool[] Mask { get; private set; }
        const int CacheSize = 0x500;
        public List<IModifier> Modifiers = new List<IModifier>();

        private bool DataCompare(byte[] data, uint dataOffset)
        {
            return !Mask.Where((t, i) => t && Bytes[i] != data[dataOffset + i]).Any();
        }

        private IntPtr FindStart(ExternalProcessReader bm)
        {
            var mainModule = bm.Process.MainModule;
            var start = mainModule.BaseAddress;
            var size = mainModule.ModuleMemorySize;
            var patternLength = Bytes.Length;

            for (uint i = 0; i < size - patternLength; i += (uint)(CacheSize - patternLength))
            {
                byte[] cache = bm.ReadBytes(start + (int)i, CacheSize > size - i ? size - (int)i : CacheSize);
                for (uint i2 = 0; i2 < (cache.Length - patternLength); i2++)
                {
                    if (DataCompare(cache, i2))
                        return start + (int)(i + i2);
                }
            }
            throw new InvalidDataException(string.Format("Pattern {0} not found",Name));
        }

        public IntPtr Find(ExternalProcessReader bm)
        {
            var start = FindStart(bm);
            start = Modifiers.Aggregate(start, (current, mod) => mod.Apply(bm, current));
            return start - (int)bm.Process.MainModule.BaseAddress;
        }

        public static Pattern FromTextstyle(string name, string pattern, params IModifier[] modifiers)
        {
            var ret = new Pattern { Name = name };
            if (modifiers != null)
                ret.Modifiers = modifiers.ToList();
            var split = pattern.Split(' ');
            int index = 0;
            ret.Bytes = new byte[split.Length];
            ret.Mask = new bool[split.Length];
            foreach (var token in split)
            {
                if (token.Length > 2)
                    throw new InvalidDataException("Invalid token: " + token);
                if (token.Contains("?"))
                    ret.Mask[index++] = false;
                else
                {
                    byte data = byte.Parse(token, NumberStyles.HexNumber);
                    ret.Bytes[index] = data;
                    ret.Mask[index] = true;
                    index++;
                }
            }
            return ret;
        }
    }
}
