﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace OpenQuantumSafe
{
    /// <summary>
    /// Offers post-quantum signature mechanisms.
    /// </summary>
    public class Sig : Mechanism
    {
        /// <summary>
        /// A structure corresponding to the native OQS_SIG struct.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct OQS_SIG
        {
            public IntPtr method_name;
            public IntPtr alg_version;
            public byte claimed_nist_level;
            public byte euf_cma;
            public UIntPtr length_public_key;
            public UIntPtr length_secret_key;
            public UIntPtr length_signature;
            private IntPtr keypair_function; // unused
            private IntPtr sign_function; // unused
            private IntPtr verify_function; // unused
        }

        #region OQS native DLL functions
        [DllImport("oqs.dll", CallingConvention = CallingConvention.Cdecl)]
        extern private static IntPtr OQS_SIG_new(string method_name);

        [DllImport("oqs.dll", CallingConvention = CallingConvention.Cdecl)]
        extern private static int OQS_SIG_keypair(IntPtr sig, byte[] public_key, byte[] secret_key);

        [DllImport("oqs.dll", CallingConvention = CallingConvention.Cdecl)]
        extern private static int OQS_SIG_sign(IntPtr sig, byte[] signature, ref UIntPtr sig_len, byte[] message, int message_len, byte[] secret_key);

        [DllImport("oqs.dll", CallingConvention = CallingConvention.Cdecl)]
        extern private static int OQS_SIG_verify(IntPtr sig, byte[] message, int message_len, byte[] signature, int signature_len, byte[] public_key);

        [DllImport("oqs.dll", CallingConvention = CallingConvention.Cdecl)]
        extern private static void OQS_SIG_free(IntPtr sig);

        [DllImport("oqs.dll", CallingConvention = CallingConvention.Cdecl)]
        extern private static IntPtr OQS_SIG_alg_identifier(int index);

        [DllImport("oqs.dll", CallingConvention = CallingConvention.Cdecl)]
        extern private static int OQS_SIG_alg_count();

        [DllImport("oqs.dll", CallingConvention = CallingConvention.Cdecl)]
        extern private static int OQS_SIG_alg_is_enabled(string method_name);

        [DllImport("liboqs", CallingConvention = CallingConvention.Cdecl, EntryPoint = "OQS_SIG_new")]
        extern private static IntPtr Lib_OQS_SIG_new(string method_name);

        [DllImport("liboqs", CallingConvention = CallingConvention.Cdecl, EntryPoint = "OQS_SIG_keypair")]
        extern private static int Lib_OQS_SIG_keypair(IntPtr sig, byte[] public_key, byte[] secret_key);

        [DllImport("liboqs", CallingConvention = CallingConvention.Cdecl, EntryPoint = "OQS_SIG_sign")]
        extern private static int Lib_OQS_SIG_sign(IntPtr sig, byte[] signature, ref UIntPtr sig_len, byte[] message, int message_len, byte[] secret_key);

        [DllImport("liboqs", CallingConvention = CallingConvention.Cdecl, EntryPoint = "OQS_SIG_verify")]
        extern private static int Lib_OQS_SIG_verify(IntPtr sig, byte[] message, int message_len, byte[] signature, int signature_len, byte[] public_key);

        [DllImport("liboqs", CallingConvention = CallingConvention.Cdecl, EntryPoint = "OQS_SIG_free")]
        extern private static void Lib_OQS_SIG_free(IntPtr sig);

        [DllImport("liboqs", CallingConvention = CallingConvention.Cdecl, EntryPoint = "OQS_SIG_alg_identifier")]
        extern private static IntPtr Lib_OQS_SIG_alg_identifier(int index);

        [DllImport("liboqs", CallingConvention = CallingConvention.Cdecl, EntryPoint = "OQS_SIG_alg_count")]
        extern private static int Lib_OQS_SIG_alg_count();

        [DllImport("liboqs", CallingConvention = CallingConvention.Cdecl, EntryPoint = "OQS_SIG_alg_is_enabled")]
        extern private static int Lib_OQS_SIG_alg_is_enabled(string method_name);
        #endregion

        /// <summary>
        /// List of supported mechanisms. Some mechanisms might have been disabled at runtime,
        /// see <see cref="EnableddMechanisms"/> for the list of enabled mechanisms.
        /// </summary>
        public static ImmutableList<string> SupportedMechanisms { get; private set; }

        /// <summary>
        /// List of enabled mechanisms.
        /// </summary>
        public static ImmutableList<string> EnabledMechanisms { get; protected set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Sig()
        {
            // initialize list of supported/enabled mechanisms
            List<string> enabled = new List<string>();
            List<string> supported = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                int count = OQS_SIG_alg_count();
                for (int i = 0; i < count; i++)
                {
                    string alg = Marshal.PtrToStringAnsi(OQS_SIG_alg_identifier(i));
                    supported.Add(alg);
                    // determine if the alg is enabled
                    if (OQS_SIG_alg_is_enabled(alg) == 1)
                    {
                        enabled.Add(alg);
                    }
                }
            }
            else
            {
                int count = Lib_OQS_SIG_alg_count();
                for (int i = 0; i < count; i++)
                {
                    string alg = Marshal.PtrToStringAnsi(Lib_OQS_SIG_alg_identifier(i));
                    supported.Add(alg);
                    // determine if the alg is enabled
                    if (Lib_OQS_SIG_alg_is_enabled(alg) == 1)
                    {
                        enabled.Add(alg);
                    }
                }
            }
            EnabledMechanisms = enabled.ToImmutableList<string>();
            SupportedMechanisms = supported.ToImmutableList<string>();
        }

        /// <summary>
        /// The OQS signature context.
        /// </summary>
        private OQS_SIG oqs_sig;

        /// <summary>
        /// Constructs a signature object.
        /// </summary>
        /// <param name="sigAlg">An OQS signature algorithm identifier; must be a value from <see cref="EnabledMechanisms"/>.</param>
        /// <exception cref="MechanismNotEnabledException">Thrown if the algorithm is supported but not enabled by the OQS runtime.</exception>
        /// <exception cref="MechanismNotSupportedException">Thrown if the algorithm is not supported by OQS.</exception>
        /// <exception cref="OQSException">Thrown if the OQS signature object can't be initialized.</exception>
        public Sig(string sigAlg)
        {
            if (!SupportedMechanisms.Contains(sigAlg))
            {
                throw new MechanismNotSupportedException(sigAlg);
            }
            if (!EnabledMechanisms.Contains(sigAlg))
            {
                throw new MechanismNotEnabledException(sigAlg);
            }
            oqs_ptr = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OQS_SIG_new(sigAlg) : Lib_OQS_SIG_new(sigAlg);
            if (oqs_ptr == IntPtr.Zero)
            {
                throw new OQSException("Failed to initialize signature mechanism");
            }
            oqs_sig = Marshal.PtrToStructure<OQS_SIG>(oqs_ptr);
        }

        /// <summary>
        /// The signature algorithm name.
        /// </summary>
        public string AlgorithmName
        {
            get
            {
                return Marshal.PtrToStringAnsi(oqs_sig.method_name);
            }
        }

        /// <summary>
        /// The algorithm version.
        /// </summary>
        public string AlgorithmVersion
        {
            get
            {
                return Marshal.PtrToStringAnsi(oqs_sig.alg_version);
            }
        }

        /// <summary>
        /// The claimed NIST security level.
        /// </summary>
        public int ClaimedNistLevel
        {
            get
            {
                return oqs_sig.claimed_nist_level;
            }
        }

        /// <summary>
        /// <code>true</code> if the scheme is EUF-CMA, <code>false</code> otherwise.
        /// </summary>
        public bool IsEufCma
        {
            get
            {
                return oqs_sig.euf_cma== 1;
            }
        }

        /// <summary>
        /// Length of the public key.
        /// </summary>
        public UInt64 PublicKeyLength
        {
            get
            {
                return (UInt64) oqs_sig.length_public_key;
            }
        }

        /// <summary>
        /// Length of the secret key.
        /// </summary>

        public UInt64 SecretKeyLength
        {
            get
            {
                return (UInt64) oqs_sig.length_secret_key;
            }
        }

        /// <summary>
        /// Length of the signature.
        /// </summary>

        public UInt64 SignatureLength
        {
            get
            {
                return (UInt64) oqs_sig.length_signature;
            }
        }

        /// <summary>
        /// Generates the signature keypair.
        /// </summary>
        /// <param name="public_key">The returned public key.</param>
        /// <param name="secret_key">The returned secret key.</param>
        public void keypair(out byte[] public_key, out byte[] secret_key)
        {
            if (oqs_ptr == IntPtr.Zero)
            {
                throw new ObjectDisposedException("Sig");
            }

            byte[] my_public_key = new byte[PublicKeyLength];
            byte[] my_secret_key = new byte[SecretKeyLength];
            int returnValue = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OQS_SIG_keypair(oqs_ptr, my_public_key, my_secret_key) : Lib_OQS_SIG_keypair(oqs_ptr, my_public_key, my_secret_key);
            if (returnValue != OpenQuantumSafe.Status.Success)
            {
                throw new OQSException(returnValue);
            }
            public_key = my_public_key;
            secret_key = my_secret_key;
        }

        /// <summary>
        /// Signs a message.
        /// </summary>
        /// <param name="signature">The returned signature.</param>
        /// <param name="message">The message to sign.</param>
        /// <param name="secret_key">The signer's secret key.</param>
        public void sign(out byte[] signature, byte[] message, byte[] secret_key)
        {
            if (oqs_ptr == IntPtr.Zero)
            {
                throw new ObjectDisposedException("Sig");
            }
            byte[] my_signature = new byte[SignatureLength];
            UIntPtr sig_len_ptr = new UIntPtr();
            int returnValue = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OQS_SIG_sign(oqs_ptr, my_signature, ref sig_len_ptr, message, message.Length, secret_key) : Lib_OQS_SIG_sign(oqs_ptr, my_signature, ref sig_len_ptr, message, message.Length, secret_key);
            if (returnValue != OpenQuantumSafe.Status.Success)
            {
                throw new OQSException(returnValue);
            }
            Array.Resize<byte>(ref my_signature, (int) sig_len_ptr.ToUInt64()); 
            signature = my_signature;
        }

        /// <summary>
        /// Verifies the signature on a message.
        /// </summary>
        /// <param name="message">The signed message.</param>
        /// <param name="signature">The signature to verify.</param>
        /// <param name="public_key">The signer's public key.</param>
        /// <returns>True if the signature is valid, false otherwise.</returns>
        public bool verify(byte[] message, byte[] signature, byte[] public_key)
        {
            if (oqs_ptr == IntPtr.Zero)
            {
                throw new ObjectDisposedException("Sig");
            }
            int returnValue = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OQS_SIG_verify(oqs_ptr, message, message.Length, signature, signature.Length, public_key) : Lib_OQS_SIG_verify(oqs_ptr, message, message.Length, signature, signature.Length, public_key);
            if (returnValue == OpenQuantumSafe.Status.Success)
            {
                return true;
            }
            else if (returnValue == OpenQuantumSafe.Status.Error)
            {
                return false;
            } else
            {
                throw new OQSException(returnValue);
            }
        }

        protected override void OQS_free(IntPtr oqs_ptr)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                OQS_SIG_free(oqs_ptr);
            }
            else
            {
                Lib_OQS_SIG_free(oqs_ptr);
            }
        }
    }
}
