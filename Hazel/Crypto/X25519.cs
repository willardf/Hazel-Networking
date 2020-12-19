using System;
using System.Diagnostics;

namespace Hazel.Crypto
{
    /// <summary>
    /// The x25519 key agreement algorithm
    /// </summary>
    public static class X25519
    {
        public const int KeySize = 32;

        /// <summary>
        /// Element in the GF(2^255 - 19) field
        /// </summary>
        public partial struct FieldElement
        {
            public int x0, x1, x2, x3, x4;
            public int x5, x6, x7, x8, x9;
        };

        private static readonly byte[] BasePoint = {9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};

        /// <summary>
        /// Performs the core x25519 function: Multiplying an EC point by a scalar value
        /// </summary>
        public static bool Func(ByteSpan output, ByteSpan scalar, ByteSpan point)
        {
            InternalFunc(output, scalar, point);
            if (Const.ConstantCompareZeroSpan(output) == 1)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Multiplies the base x25519 point by the provided scalar value
        /// </summary>
        public static void Func(ByteSpan output, ByteSpan scalar)
        {
            InternalFunc(output, scalar, BasePoint);
        }

        // The FieldElement code below is ported from the original
        // public domain reference implemtation of X25519
        // by D. J. Bernstien
        //
        // See: https://cr.yp.to/ecdh.html

        private static void InternalFunc(ByteSpan output, ByteSpan scalar, ByteSpan point)
        {
            if (output.Length != KeySize)
            {
                throw new ArgumentException("Invalid output size", nameof(output));
            }
            else if (scalar.Length != KeySize)
            {
                throw new ArgumentException("Invalid scalar size", nameof(scalar));
            }
            else if (point.Length != KeySize)
            {
                throw new ArgumentException("Invalid point size", nameof(point));
            }

            // copy the scalar so we can properly mask it
            ByteSpan maskedScalar = new byte[32];
            scalar.CopyTo(maskedScalar);
            maskedScalar[0] &= 248;
            maskedScalar[31] &= 127;
            maskedScalar[31] |= 64;

            FieldElement x1 = FieldElement.FromBytes(point);
            FieldElement x2 = FieldElement.One();
            FieldElement x3 = x1;
            FieldElement z2 = FieldElement.Zero();
            FieldElement z3 = FieldElement.One();

            FieldElement tmp0 = new FieldElement();
            FieldElement tmp1 = new FieldElement();

            int swap = 0;
            for (int pos = 254; pos >= 0; --pos)
            {
                int b = (int)maskedScalar[pos / 8] >> (int)(pos % 8);
                b &= 1;
                swap ^= b;

                FieldElement.ConditionalSwap(ref x2, ref x3, swap);
                FieldElement.ConditionalSwap(ref z2, ref z3, swap);
                swap = b;

                FieldElement.Sub(ref tmp0, ref x3, ref z3);
                FieldElement.Sub(ref tmp1, ref x2, ref z2);
                FieldElement.Add(ref x2, ref x2, ref z2);
                FieldElement.Add(ref z2, ref x3, ref z3);
                FieldElement.Multiply(ref z3, ref tmp0, ref x2);
                FieldElement.Multiply(ref z2, ref z2, ref tmp1);
                FieldElement.Square(ref tmp0, ref tmp1);
                FieldElement.Square(ref tmp1, ref x2);
                FieldElement.Add(ref x3, ref z3, ref z2);
                FieldElement.Sub(ref z2, ref z3, ref z2);
                FieldElement.Multiply(ref x2, ref tmp1, ref tmp0);
                FieldElement.Sub(ref tmp1, ref tmp1, ref tmp0);
                FieldElement.Square(ref z2, ref z2);
                FieldElement.Multiply121666(ref z3, ref tmp1);
                FieldElement.Square(ref x3, ref x3);
                FieldElement.Add(ref tmp0, ref tmp0, ref z3);
                FieldElement.Multiply(ref z3, ref x1, ref z2);
                FieldElement.Multiply(ref z2, ref tmp1, ref tmp0);
            }

            FieldElement.ConditionalSwap(ref x2, ref x3, swap);
            FieldElement.ConditionalSwap(ref z2, ref z3, swap);

            FieldElement.Invert(ref z2, ref z2);
            FieldElement.Multiply(ref x2, ref x2, ref z2);
            x2.CopyTo(output);
        }


        /// <summary>
        /// Mathematical operators over GF(2^255 - 19)
        /// </summary>
        partial struct FieldElement
        {
            /// <summary>
            /// Convert a byte array to a field element
            /// </summary>
            public static FieldElement FromBytes(ByteSpan bytes)
            {
                Debug.Assert(bytes.Length >= KeySize);

                long tmp0 = (long)bytes.ReadLittleEndian32();
                long tmp1 = (long)bytes.ReadLittleEndian24(4) << 6;
                long tmp2 = (long)bytes.ReadLittleEndian24(7) << 5;
                long tmp3 = (long)bytes.ReadLittleEndian24(10) << 3;
                long tmp4 = (long)bytes.ReadLittleEndian24(13) << 2;
                long tmp5 = (long)bytes.ReadLittleEndian32(16);
                long tmp6 = (long)bytes.ReadLittleEndian24(20) << 7;
                long tmp7 = (long)bytes.ReadLittleEndian24(23) << 5;
                long tmp8 = (long)bytes.ReadLittleEndian24(26) << 4;
                long tmp9 = (long)(bytes.ReadLittleEndian24(29) & 0x007FFFFF) << 2;

                long carry9 = (tmp9 + (1L<<24)) >> 25;
                tmp0 += carry9 * 19;
                tmp9 -= carry9 << 25;
                long carry1 = (tmp1 + (1L<<24)) >> 25;
                tmp2 += carry1;
                tmp1 -= carry1 << 25;
                long carry3 = (tmp3 + (1L<<24)) >> 25;
                tmp4 += carry3;
                tmp3 -= carry3 << 25;
                long carry5 = (tmp5 + (1L<<24)) >> 25;
                tmp6 += carry5;
                tmp5 -= carry5 << 25;
                long carry7 = (tmp7 + (1L<<24)) >> 25;
                tmp8 += carry7;
                tmp7 -= carry7 << 25;

                long carry0 = (tmp0 + (1L<<25)) >> 26;
                tmp1 += carry0;
                tmp0 -= carry0 << 26;
                long carry2 = (tmp2 + (1L<<25)) >> 26;
                tmp3 += carry2;
                tmp2 -= carry2 << 26;
                long carry4 = (tmp4 + (1L<<25)) >> 26;
                tmp5 += carry4;
                tmp4 -= carry4 << 26;
                long carry6 = (tmp6 + (1L<<25)) >> 26;
                tmp7 += carry6;
                tmp6 -= carry6 << 26;
                long carry8 = (tmp8 + (1L<<25)) >> 26;
                tmp9 += carry8;
                tmp8 -= carry8 << 26;

                return new FieldElement
                {
                    x0 = (int)tmp0,
                    x1 = (int)tmp1,
                    x2 = (int)tmp2,
                    x3 = (int)tmp3,
                    x4 = (int)tmp4,
                    x5 = (int)tmp5,
                    x6 = (int)tmp6,
                    x7 = (int)tmp7,
                    x8 = (int)tmp8,
                    x9 = (int)tmp9,
                };
            }

            /// <summary>
            /// Convert the field element to a byte array
            /// </summary>
            public void CopyTo(ByteSpan output)
            {
                Debug.Assert(output.Length >= 32);

                long q = (19 * this.x9 + (1L << 24)) >> 25;
                q = ((long)this.x0 + q) >> 26;
                q = ((long)this.x1 + q) >> 25;
                q = ((long)this.x2 + q) >> 26;
                q = ((long)this.x3 + q) >> 25;
                q = ((long)this.x4 + q) >> 26;
                q = ((long)this.x5 + q) >> 25;
                q = ((long)this.x6 + q) >> 26;
                q = ((long)this.x7 + q) >> 25;
                q = ((long)this.x8 + q) >> 26;
                q = ((long)this.x9 + q) >> 25;

                this.x0 = (int)((long)this.x0 + (19L * q));

                int carry0 = (int)(this.x0 >> 26);
                this.x1 = (int)((int)this.x1 + carry0);
                this.x0 = (int)((int)this.x0 - (carry0 << 26));
                int carry1 = (int)(this.x1 >> 25);
                this.x2 = (int)((int)this.x2 + carry1);
                this.x1 = (int)((int)this.x1 - (carry1 << 25));
                int carry2 = (int)(this.x2 >> 26);
                this.x3 = (int)((int)this.x3 + carry2);
                this.x2 = (int)((int)this.x2 - (carry2 << 26));
                int carry3 = (int)(this.x3 >> 25);
                this.x4 = (int)((int)this.x4 + carry3);
                this.x3 = (int)((int)this.x3 - (carry3 << 25));
                int carry4 = (int)(this.x4 >> 26);
                this.x5 = (int)((int)this.x5 + carry4);
                this.x4 = (int)((int)this.x4 - (carry4 << 26));
                int carry5 = (int)(this.x5 >> 25);
                this.x6 = (int)((int)this.x6 + carry5);
                this.x5 = (int)((int)this.x5 - (carry5 << 25));
                int carry6 = (int)(this.x6 >> 26);
                this.x7 = (int)((int)this.x7 + carry6);
                this.x6 = (int)((int)this.x6 - (carry6 << 26));
                int carry7 = (int)(this.x7 >> 25);
                this.x8 = (int)((int)this.x8 + carry7);
                this.x7 = (int)((int)this.x7 - (carry7 << 25));
                int carry8 = (int)(this.x8 >> 26);
                this.x9 = (int)((int)this.x9 + carry8);
                this.x8 = (int)((int)this.x8 - (carry8 << 26));
                int carry9 = (int)(this.x9 >> 25);
                this.x9 = (int)((int)this.x9 - (carry9 << 25));

                output[ 0] = (byte)(this.x0 >> 0);
                output[ 1] = (byte)(this.x0 >> 8);
                output[ 2] = (byte)(this.x0 >> 16);
                output[ 3] = (byte)((this.x0 >> 24) | (this.x1 << 2));
                output[ 4] = (byte)(this.x1 >> 6);
                output[ 5] = (byte)(this.x1 >> 14);
                output[ 6] = (byte)((this.x1 >> 22) | (this.x2 << 3));
                output[ 7] = (byte)(this.x2 >> 5);
                output[ 8] = (byte)(this.x2 >> 13);
                output[ 9] = (byte)((this.x2 >> 21) | (this.x3 << 5));
                output[10] = (byte)(this.x3 >> 3);
                output[11] = (byte)(this.x3 >> 11);
                output[12] = (byte)((this.x3 >> 19) | (this.x4 << 6));
                output[13] = (byte)(this.x4 >> 2);
                output[14] = (byte)(this.x4 >> 10);
                output[15] = (byte)(this.x4 >> 18);
                output[16] = (byte)(this.x5 >> 0);
                output[17] = (byte)(this.x5 >> 8);
                output[18] = (byte)(this.x5 >> 16);
                output[19] = (byte)((this.x5 >> 24) | (this.x6 << 1));
                output[20] = (byte)(this.x6 >> 7);
                output[21] = (byte)(this.x6 >> 15);
                output[22] = (byte)((this.x6 >> 23) | (this.x7 << 3));
                output[23] = (byte)(this.x7 >> 5);
                output[24] = (byte)(this.x7 >> 13);
                output[25] = (byte)((this.x7 >> 21) | (this.x8 << 4));
                output[26] = (byte)(this.x8 >> 4);
                output[27] = (byte)(this.x8 >> 12);
                output[28] = (byte)((this.x8 >> 20) | (this.x9 << 6));
                output[29] = (byte)(this.x9 >> 2);
                output[30] = (byte)(this.x9 >> 10);
                output[31] = (byte)(this.x9 >> 18);
            }

            /// <summary>
            /// Set the field element to `0`
            /// </summary>
            public static FieldElement Zero()
            {
                return new FieldElement();
            }

            /// <summary>
            /// Set the field element to `1`
            /// </summary>
            public static FieldElement One()
            {
                FieldElement result = Zero();
                result.x0 = 1;
                return result;
            }

            /// <summary>
            /// Add two field elements
            /// </summary>
            public static void Add(ref FieldElement output, ref FieldElement a, ref FieldElement b)
            {
                output.x0 = a.x0 + b.x0;
                output.x1 = a.x1 + b.x1;
                output.x2 = a.x2 + b.x2;
                output.x3 = a.x3 + b.x3;
                output.x4 = a.x4 + b.x4;
                output.x5 = a.x5 + b.x5;
                output.x6 = a.x6 + b.x6;
                output.x7 = a.x7 + b.x7;
                output.x8 = a.x8 + b.x8;
                output.x9 = a.x9 + b.x9;
            }

            /// <summary>
            /// Subtract two field elements
            /// </summary>
            public static void Sub(ref FieldElement output, ref FieldElement a, ref FieldElement b)
            {
                output.x0 = a.x0 - b.x0;
                output.x1 = a.x1 - b.x1;
                output.x2 = a.x2 - b.x2;
                output.x3 = a.x3 - b.x3;
                output.x4 = a.x4 - b.x4;
                output.x5 = a.x5 - b.x5;
                output.x6 = a.x6 - b.x6;
                output.x7 = a.x7 - b.x7;
                output.x8 = a.x8 - b.x8;
                output.x9 = a.x9 - b.x9;
            }

            /// <summary>
            /// Multiply two field elements
            /// </summary>
            public static void Multiply(ref FieldElement output, ref FieldElement a, ref FieldElement b)
            {
                int b1_19 = 19 * b.x1;
                int b2_19 = 19 * b.x2;
                int b3_19 = 19 * b.x3;
                int b4_19 = 19 * b.x4;
                int b5_19 = 19 * b.x5;
                int b6_19 = 19 * b.x6;
                int b7_19 = 19 * b.x7;
                int b8_19 = 19 * b.x8;
                int b9_19 = 19 * b.x9;

                int a1_2 = 2 * a.x1;
                int a3_2 = 2 * a.x3;
                int a5_2 = 2 * a.x5;
                int a7_2 = 2 * a.x7;
                int a9_2 = 2 * a.x9;

                long a0b0 = (long)a.x0 * (long)b.x0;
                long a0b1 = (long)a.x0 * (long)b.x1;
                long a0b2 = (long)a.x0 * (long)b.x2;
                long a0b3 = (long)a.x0 * (long)b.x3;
                long a0b4 = (long)a.x0 * (long)b.x4;
                long a0b5 = (long)a.x0 * (long)b.x5;
                long a0b6 = (long)a.x0 * (long)b.x6;
                long a0b7 = (long)a.x0 * (long)b.x7;
                long a0b8 = (long)a.x0 * (long)b.x8;
                long a0b9 = (long)a.x0 * (long)b.x9;
                long a1b0 = (long)a.x1 * (long)b.x0;
                long a1b1_2 = (long)a1_2 * (long)b.x1;
                long a1b2 = (long)a.x1 * (long)b.x2;
                long a1b3_2 = (long)a1_2 * (long)b.x3;
                long a1b4 = (long)a.x1 * (long)b.x4;
                long a1b5_2 = (long)a1_2 * (long)b.x5;
                long a1b6 = (long)a.x1 * (long)b.x6;
                long a1b7_2 = (long)a1_2 * (long)b.x7;
                long a1b8 = (long)a.x1 * (long)b.x8;
                long a1b9_38 = (long)a1_2 * (long)b9_19;
                long a2b0 = (long)a.x2 * (long)b.x0;
                long a2b1 = (long)a.x2 * (long)b.x1;
                long a2b2 = (long)a.x2 * (long)b.x2;
                long a2b3 = (long)a.x2 * (long)b.x3;
                long a2b4 = (long)a.x2 * (long)b.x4;
                long a2b5 = (long)a.x2 * (long)b.x5;
                long a2b6 = (long)a.x2 * (long)b.x6;
                long a2b7 = (long)a.x2 * (long)b.x7;
                long a2b8_19 = (long)a.x2 * (long)b8_19;
                long a2b9_19 = (long)a.x2 * (long)b9_19;
                long a3b0 = (long)a.x3 * (long)b.x0;
                long a3b1_2 = (long)a3_2 * (long)b.x1;
                long a3b2 = (long)a.x3 * (long)b.x2;
                long a3b3_2 = (long)a3_2 * (long)b.x3;
                long a3b4 = (long)a.x3 * (long)b.x4;
                long a3b5_2 = (long)a3_2 * (long)b.x5;
                long a3b6 = (long)a.x3 * (long)b.x6;
                long a3b7_38 = (long)a3_2 * (long)b7_19;
                long a3b8_19 = (long)a.x3 * (long)b8_19;
                long a3b9_38 = (long)a3_2 * (long)b9_19;
                long a4b0 = (long)a.x4 * (long)b.x0;
                long a4b1 = (long)a.x4 * (long)b.x1;
                long a4b2 = (long)a.x4 * (long)b.x2;
                long a4b3 = (long)a.x4 * (long)b.x3;
                long a4b4 = (long)a.x4 * (long)b.x4;
                long a4b5 = (long)a.x4 * (long)b.x5;
                long a4b6_19 = (long)a.x4 * (long)b6_19;
                long a4b7_19 = (long)a.x4 * (long)b7_19;
                long a4b8_19 = (long)a.x4 * (long)b8_19;
                long a4b9_19 = (long)a.x4 * (long)b9_19;
                long a5b0 = (long)a.x5 * (long)b.x0;
                long a5b1_2 = (long)a5_2 * (long)b.x1;
                long a5b2 = (long)a.x5 * (long)b.x2;
                long a5b3_2 = (long)a5_2 * (long)b.x3;
                long a5b4 = (long)a.x5 * (long)b.x4;
                long a5b5_38 = (long)a5_2 * (long)b5_19;
                long a5b6_19 = (long)a.x5 * (long)b6_19;
                long a5b7_38 = (long)a5_2 * (long)b7_19;
                long a5b8_19 = (long)a.x5 * (long)b8_19;
                long a5b9_38 = (long)a5_2 * (long)b9_19;
                long a6b0 = (long)a.x6 * (long)b.x0;
                long a6b1 = (long)a.x6 * (long)b.x1;
                long a6b2 = (long)a.x6 * (long)b.x2;
                long a6b3 = (long)a.x6 * (long)b.x3;
                long a6b4_19 = (long)a.x6 * (long)b4_19;
                long a6b5_19 = (long)a.x6 * (long)b5_19;
                long a6b6_19 = (long)a.x6 * (long)b6_19;
                long a6b7_19 = (long)a.x6 * (long)b7_19;
                long a6b8_19 = (long)a.x6 * (long)b8_19;
                long a6b9_19 = (long)a.x6 * (long)b9_19;
                long a7b0 = (long)a.x7 * (long)b.x0;
                long a7b1_2 = (long)a7_2 * (long)b.x1;
                long a7b2 = (long)a.x7 * (long)b.x2;
                long a7b3_38 = (long)a7_2 * (long)b3_19;
                long a7b4_19 = (long)a.x7 * (long)b4_19;
                long a7b5_38 = (long)a7_2 * (long)b5_19;
                long a7b6_19 = (long)a.x7 * (long)b6_19;
                long a7b7_38 = (long)a7_2 * (long)b7_19;
                long a7b8_19 = (long)a.x7 * (long)b8_19;
                long a7b9_38 = (long)a7_2 * (long)b9_19;
                long a8b0 = (long)a.x8 * (long)b.x0;
                long a8b1 = (long)a.x8 * (long)b.x1;
                long a8b2_19 = (long)a.x8 * (long)b2_19;
                long a8b3_19 = (long)a.x8 * (long)b3_19;
                long a8b4_19 = (long)a.x8 * (long)b4_19;
                long a8b5_19 = (long)a.x8 * (long)b5_19;
                long a8b6_19 = (long)a.x8 * (long)b6_19;
                long a8b7_19 = (long)a.x8 * (long)b7_19;
                long a8b8_19 = (long)a.x8 * (long)b8_19;
                long a8b9_19 = (long)a.x8 * (long)b9_19;
                long a9b0 = (long)a.x9 * (long)b.x0;
                long a9b1_38 = (long)a9_2 * (long)b1_19;
                long a9b2_19 = (long)a.x9 * (long)b2_19;
                long a9b3_38 = (long)a9_2 * (long)b3_19;
                long a9b4_19 = (long)a.x9 * (long)b4_19;
                long a9b5_38 = (long)a9_2 * (long)b5_19;
                long a9b6_19 = (long)a.x9 * (long)b6_19;
                long a9b7_38 = (long)a9_2 * (long)b7_19;
                long a9b8_19 = (long)a.x9 * (long)b8_19;
                long a9b9_38 = (long)a9_2 * (long)b9_19;

                long h0 = a0b0 + a1b9_38 + a2b8_19 + a3b7_38 + a4b6_19 + a5b5_38 + a6b4_19 + a7b3_38 + a8b2_19 + a9b1_38;
                long h1 = a0b1 + a1b0 + a2b9_19 + a3b8_19 + a4b7_19 + a5b6_19 + a6b5_19 + a7b4_19 + a8b3_19 + a9b2_19;
                long h2 = a0b2 + a1b1_2 + a2b0 + a3b9_38 + a4b8_19 + a5b7_38 + a6b6_19 + a7b5_38 + a8b4_19 + a9b3_38;
                long h3 = a0b3 + a1b2 + a2b1 + a3b0 + a4b9_19 + a5b8_19 + a6b7_19 + a7b6_19 + a8b5_19 + a9b4_19;
                long h4 = a0b4 + a1b3_2 + a2b2 + a3b1_2 + a4b0 + a5b9_38 + a6b8_19 + a7b7_38 + a8b6_19 + a9b5_38;
                long h5 = a0b5 + a1b4 + a2b3 + a3b2 + a4b1 + a5b0 + a6b9_19 + a7b8_19 + a8b7_19 + a9b6_19;
                long h6 = a0b6 + a1b5_2 + a2b4 + a3b3_2 + a4b2 + a5b1_2 + a6b0 + a7b9_38 + a8b8_19 + a9b7_38;
                long h7 = a0b7 + a1b6 + a2b5 + a3b4 + a4b3 + a5b2 + a6b1 + a7b0 + a8b9_19 + a9b8_19;
                long h8 = a0b8 + a1b7_2 + a2b6 + a3b5_2 + a4b4 + a5b3_2 + a6b2 + a7b1_2 + a8b0 + a9b9_38;
                long h9 = a0b9 + a1b8 + a2b7 + a3b6 + a4b5 + a5b4 + a6b3 + a7b2 + a8b1 + a9b0;

                long carry0 = (h0 + (1L << 25)) >> 26;
                h1 += carry0;
                h0 -= carry0 << 26;
                long carry4 = (h4 + (1L << 25)) >> 26;
                h5 += carry4;
                h4 -= carry4 << 26;

                long carry1 = (h1 + (1L << 24)) >> 25;
                h2 += carry1;
                h1 -= carry1 << 25;
                long carry5 = (h5 + (1L << 24)) >> 25;
                h6 += carry5;
                h5 -= carry5 << 25;

                long carry2 = (h2 + (1L << 25)) >> 26;
                h3 += carry2;
                h2 -= carry2 << 26;
                long carry6 = (h6 + (1L << 25)) >> 26;
                h7 += carry6;
                h6 -= carry6 << 26;

                long carry3 = (h3 + (1L << 24)) >> 25;
                h4 += carry3;
                h3 -= carry3 << 25;
                long carry7 = (h7 + (1L << 24)) >> 25;
                h8 += carry7;
                h7 -= carry7 << 25;

                carry4 = (h4 + (1L << 25)) >> 26;
                h5 += carry4;
                h4 -= carry4 << 26;
                long carry8 = (h8 + (1L << 25)) >> 26;
                h9 += carry8;
                h8 -= carry8 << 26;

                long carry9 = (h9 + (1L << 24)) >> 25;
                h0 += carry9 * 19;
                h9 -= carry9 << 25;

                carry0 = (h0 + (1L << 25)) >> 26;
                h1 += carry0;
                h0 -= carry0 << 26;

                output.x0 = (int)h0;
                output.x1 = (int)h1;
                output.x2 = (int)h2;
                output.x3 = (int)h3;
                output.x4 = (int)h4;
                output.x5 = (int)h5;
                output.x6 = (int)h6;
                output.x7 = (int)h7;
                output.x8 = (int)h8;
                output.x9 = (int)h9;
            }

            /// <summary>
            /// Square a field element
            /// </summary>
            public static void Square(ref FieldElement output, ref FieldElement a)
            {
                int a0_2 = a.x0 * 2;
                int a1_2 = a.x1 * 2;
                int a2_2 = a.x2 * 2;
                int a3_2 = a.x3 * 2;
                int a4_2 = a.x4 * 2;
                int a5_2 = a.x5 * 2;
                int a6_2 = a.x6 * 2;
                int a7_2 = a.x7 * 2;

                int a5_38 = a.x5 * 38;
                int a6_19 = a.x6 * 19;
                int a7_38 = a.x7 * 38;
                int a8_19 = a.x8 * 19;
                int a9_38 = a.x9 * 38;

                long a0a0 = (long)a.x0 * (long)a.x0;
                long a0a1_2 = (long)a0_2 * (long)a.x1;
                long a0a2_2 = (long)a0_2 * (long)a.x2;
                long a0a3_2 = (long)a0_2 * (long)a.x3;
                long a0a4_2 = (long)a0_2 * (long)a.x4;
                long a0a5_2 = (long)a0_2 * (long)a.x5;
                long a0a6_2 = (long)a0_2 * (long)a.x6;
                long a0a7_2 = (long)a0_2 * (long)a.x7;
                long a0a8_2 = (long)a0_2 * (long)a.x8;
                long a0a9_2 = (long)a0_2 * (long)a.x9;
                long a1a1_2 = (long)a1_2 * (long)a.x1;
                long a1a2_2 = (long)a1_2 * (long)a.x2;
                long a1a3_4 = (long)a1_2 * (long)a3_2;
                long a1a4_2 = (long)a1_2 * (long)a.x4;
                long a1a5_4 = (long)a1_2 * (long)a5_2;
                long a1a6_2 = (long)a1_2 * (long)a.x6;
                long a1a7_4 = (long)a1_2 * (long)a7_2;
                long a1a8_2 = (long)a1_2 * (long)a.x8;
                long a1a9_76 = (long)a1_2 * (long)a9_38;
                long a2a2 = (long)a.x2 * (long)a.x2;
                long a2a3_2 = (long)a2_2 * (long)a.x3;
                long a2a4_2 = (long)a2_2 * (long)a.x4;
                long a2a5_2 = (long)a2_2 * (long)a.x5;
                long a2a6_2 = (long)a2_2 * (long)a.x6;
                long a2a7_2 = (long)a2_2 * (long)a.x7;
                long a2a8_38 = (long)a2_2 * (long)a8_19;
                long a2a9_38 = (long)a.x2 * (long)a9_38;
                long a3a3_2 = (long)a3_2 * (long)a.x3;
                long a3a4_2 = (long)a3_2 * (long)a.x4;
                long a3a5_4 = (long)a3_2 * (long)a5_2;
                long a3a6_2 = (long)a3_2 * (long)a.x6;
                long a3a7_76 = (long)a3_2 * (long)a7_38;
                long a3a8_38 = (long)a3_2 * (long)a8_19;
                long a3a9_76 = (long)a3_2 * (long)a9_38;
                long a4a4 = (long)a.x4 * (long)a.x4;
                long a4a5_2 = (long)a4_2 * (long)a.x5;
                long a4a6_38 = (long)a4_2 * (long)a6_19;
                long a4a7_38 = (long)a.x4 * (long)a7_38;
                long a4a8_38 = (long)a4_2 * (long)a8_19;
                long a4a9_38 = (long)a.x4 * (long)a9_38;
                long a5a5_38 = (long)a.x5 * (long)a5_38;
                long a5a6_38 = (long)a5_2 * (long)a6_19;
                long a5a7_76 = (long)a5_2 * (long)a7_38;
                long a5a8_38 = (long)a5_2 * (long)a8_19;
                long a5a9_76 = (long)a5_2 * (long)a9_38;
                long a6a6_19 = (long)a.x6 * (long)a6_19;
                long a6a7_38 = (long)a.x6 * (long)a7_38;
                long a6a8_38 = (long)a6_2 * (long)a8_19;
                long a6a9_38 = (long)a.x6 * (long)a9_38;
                long a7a7_38 = (long)a.x7 * (long)a7_38;
                long a7a8_38 = (long)a7_2 * (long)a8_19;
                long a7a9_76 = (long)a7_2 * (long)a9_38;
                long a8a8_19 = (long)a.x8 * (long)a8_19;
                long a8a9_38 = (long)a.x8 * (long)a9_38;
                long a9a9_38 = (long)a.x9 * (long)a9_38;

                long h0 = a0a0 + a1a9_76 + a2a8_38 + a3a7_76 + a4a6_38 + a5a5_38;
                long h1 = a0a1_2 + a2a9_38 + a3a8_38 + a4a7_38 + a5a6_38;
                long h2 = a0a2_2 + a1a1_2 + a3a9_76 + a4a8_38 + a5a7_76 + a6a6_19;
                long h3 = a0a3_2 + a1a2_2 + a4a9_38 + a5a8_38 + a6a7_38;
                long h4 = a0a4_2 + a1a3_4 + a2a2 + a5a9_76 + a6a8_38 + a7a7_38;
                long h5 = a0a5_2 + a1a4_2 + a2a3_2 + a6a9_38 + a7a8_38;
                long h6 = a0a6_2 + a1a5_4 + a2a4_2 + a3a3_2 + a7a9_76 + a8a8_19;
                long h7 = a0a7_2 + a1a6_2 + a2a5_2 + a3a4_2 + a8a9_38;
                long h8 = a0a8_2 + a1a7_4 + a2a6_2 + a3a5_4 + a4a4 + a9a9_38;
                long h9 = a0a9_2 + a1a8_2 + a2a7_2 + a3a6_2 + a4a5_2;

                long carry0 = (h0 + (1L << 25)) >> 26;
                h1 += carry0;
                h0 -= carry0 << 26;
                long carry4 = (h4 + (1L << 25)) >> 26;
                h5 += carry4;
                h4 -= carry4 << 26;

                long carry1 = (h1 + (1L << 24)) >> 25;
                h2 += carry1;
                h1 -= carry1 << 25;
                long carry5 = (h5 + (1L << 24)) >> 25;
                h6 += carry5;
                h5 -= carry5 << 25;

                long carry2 = (h2 + (1L << 25)) >> 26;
                h3 += carry2;
                h2 -= carry2 << 26;
                long carry6 = (h6 + (1L << 25)) >> 26;
                h7 += carry6;
                h6 -= carry6 << 26;

                long carry3 = (h3 + (1L << 24)) >> 25;
                h4 += carry3;
                h3 -= carry3 << 25;
                long carry7 = (h7 + (1L << 24)) >> 25;
                h8 += carry7;
                h7 -= carry7 << 25;

                carry4 = (h4 + (1L << 25)) >> 26;
                h5 += carry4;
                h4 -= carry4 << 26;
                long carry8 = (h8 + (1L << 25)) >> 26;
                h9 += carry8;
                h8 -= carry8 << 26;

                long carry9 = (h9 + (1L << 24)) >> 25;
                h0 += carry9 * 19;
                h9 -= carry9 << 25;

                carry0 = (h0 + (1L << 25)) >> 26;
                h1 += carry0;
                h0 -= carry0 << 26;

                output.x0 = (int)h0;
                output.x1 = (int)h1;
                output.x2 = (int)h2;
                output.x3 = (int)h3;
                output.x4 = (int)h4;
                output.x5 = (int)h5;
                output.x6 = (int)h6;
                output.x7 = (int)h7;
                output.x8 = (int)h8;
                output.x9 = (int)h9;
            }

            /// <summary>
            /// Multiplay a field element by 121666
            /// </summary>
            public static void Multiply121666(ref FieldElement output, ref FieldElement a)
            {
                long h0 = (long)a.x0 * 121666L;
                long h1 = (long)a.x1 * 121666L;
                long h2 = (long)a.x2 * 121666L;
                long h3 = (long)a.x3 * 121666L;
                long h4 = (long)a.x4 * 121666L;
                long h5 = (long)a.x5 * 121666L;
                long h6 = (long)a.x6 * 121666L;
                long h7 = (long)a.x7 * 121666L;
                long h8 = (long)a.x8 * 121666L;
                long h9 = (long)a.x9 * 121666L;

                long carry9 = (h9 + (1L<<24)) >> 25;
                h0 += carry9 * 19;
                h9 -= carry9 << 25;
                long carry1 = (h1 + (1L<<24)) >> 25;
                h2 += carry1;
                h1 -= carry1 << 25;
                long carry3 = (h3 + (1L<<24)) >> 25;
                h4 += carry3;
                h3 -= carry3 << 25;
                long carry5 = (h5 + (1L<<24)) >> 25;
                h6 += carry5;
                h5 -= carry5 << 25;
                long carry7 = (h7 + (1L<<24)) >> 25;
                h8 += carry7;
                h7 -= carry7 << 25;

                long carry0 = (h0 + (1L << 25)) >> 26;
                h1 += carry0;
                h0 -= carry0 << 26;
                long carry2 = (h2 + (1L << 25)) >> 26;
                h3 += carry2;
                h2 -= carry2 << 26;
                long carry4 = (h4 + (1L << 25)) >> 26;
                h5 += carry4;
                h4 -= carry4 << 26;
                long carry6 = (h6 + (1L << 25)) >> 26;
                h7 += carry6;
                h6 -= carry6 << 26;
                long carry8 = (h8 + (1L << 25)) >> 26;
                h9 += carry8;
                h8 -= carry8 << 26;

                output.x0 = (int)h0;
                output.x1 = (int)h1;
                output.x2 = (int)h2;
                output.x3 = (int)h3;
                output.x4 = (int)h4;
                output.x5 = (int)h5;
                output.x6 = (int)h6;
                output.x7 = (int)h7;
                output.x8 = (int)h8;
                output.x9 = (int)h9;
            }

            /// <summary>
            /// Invert a field element
            /// </summary>
            public static void Invert(ref FieldElement output, ref FieldElement a)
            {
                FieldElement t0 = new FieldElement();
                Square(ref t0, ref a);

                FieldElement t1 = new FieldElement();
                Square(ref t1, ref t0);
                Square(ref t1, ref t1);

                FieldElement t2= new FieldElement();
                Multiply(ref t1, ref a, ref t1);
                Multiply(ref t0, ref t0, ref t1);
                Square(ref t2, ref t0);
                //Square(ref t2, ref t2);

                Multiply(ref t1, ref t1, ref t2);
                Square(ref t2, ref t1);
                for (int ii = 1; ii < 5; ++ii)
                {
                    Square(ref t2, ref t2);
                }

                Multiply(ref t1, ref t2, ref t1);
                Square(ref t2, ref t1);
                for (int ii = 1; ii < 10; ++ii)
                {
                    Square(ref t2, ref t2);
                }

                FieldElement t3 = new FieldElement();
                Multiply(ref t2, ref t2, ref t1);
                Square(ref t3, ref t2);
                for (int ii = 1; ii < 20; ++ii)
                {
                    Square(ref t3, ref t3);
                }

                Multiply(ref t2, ref t3, ref t2);
                Square(ref t2, ref t2);
                for (int ii = 1; ii < 10; ++ii)
                {
                    Square(ref t2, ref t2);
                }

                Multiply(ref t1, ref t2, ref t1);
                Square(ref t2, ref t1);
                for (int ii = 1; ii < 50; ++ii)
                {
                    Square(ref t2, ref t2);
                }

                Multiply(ref t2, ref t2, ref t1);
                Square(ref t3, ref t2);
                for (int ii = 1; ii < 100; ++ii)
                {
                    Square(ref t3, ref t3);
                }

                Multiply(ref t2, ref t3, ref t2);
                Square(ref t2, ref t2);
                for (int ii = 1; ii < 50; ++ii)
                {
                    Square(ref t2, ref t2);
                }

                Multiply(ref t1, ref t2, ref t1);
                Square(ref t1, ref t1);
                for (int ii = 1; ii < 5; ++ii)
                {
                    Square(ref t1, ref t1);
                }

                Multiply(ref output, ref t1, ref t0);
            }

            /// <summary>
            /// Swaps `a` and `b` if `swap` is 1
            /// </summary>
            public static void ConditionalSwap(ref FieldElement a, ref FieldElement b, int swap)
            {
                Debug.Assert(swap == 0 || swap == 1);
                swap = -swap;

                FieldElement temp = new FieldElement
                {
                    x0 = swap & (a.x0 ^ b.x0),
                    x1 = swap & (a.x1 ^ b.x1),
                    x2 = swap & (a.x2 ^ b.x2),
                    x3 = swap & (a.x3 ^ b.x3),
                    x4 = swap & (a.x4 ^ b.x4),
                    x5 = swap & (a.x5 ^ b.x5),
                    x6 = swap & (a.x6 ^ b.x6),
                    x7 = swap & (a.x7 ^ b.x7),
                    x8 = swap & (a.x8 ^ b.x8),
                    x9 = swap & (a.x9 ^ b.x9),
                };

                a.x0 ^= temp.x0;
                a.x1 ^= temp.x1;
                a.x2 ^= temp.x2;
                a.x3 ^= temp.x3;
                a.x4 ^= temp.x4;
                a.x5 ^= temp.x5;
                a.x6 ^= temp.x6;
                a.x7 ^= temp.x7;
                a.x8 ^= temp.x8;
                a.x9 ^= temp.x9;

                b.x0 ^= temp.x0;
                b.x1 ^= temp.x1;
                b.x2 ^= temp.x2;
                b.x3 ^= temp.x3;
                b.x4 ^= temp.x4;
                b.x5 ^= temp.x5;
                b.x6 ^= temp.x6;
                b.x7 ^= temp.x7;
                b.x8 ^= temp.x8;
                b.x9 ^= temp.x9;
            }
        }
    }
}
