using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Gibraltar;
using JetBrains.Annotations;
using NDepCheck.Reading;

namespace NDepCheck {
    public class ItemType : IEquatable<ItemType> {
        private readonly static Dictionary<string, ItemType> _allTypes = new Dictionary<string, ItemType>();

        [NotNull]
        public static readonly ItemType SIMPLE = New("SIMPLE", new[] { "Name" }, new[] { "" }, matchesOnFieldNr: true);

        private static void ForceLoadingPredefinedTypes() {
            _allTypes[SIMPLE.Name] = SIMPLE;
            _allTypes[DotNetAssemblyDependencyReaderFactory.DOTNETCALL.Name] = DotNetAssemblyDependencyReaderFactory.DOTNETCALL;
            _allTypes[DotNetAssemblyDependencyReaderFactory.DOTNETREF.Name] = DotNetAssemblyDependencyReaderFactory.DOTNETREF;
        }

        [NotNull]
        public static ItemType Generic(int fieldNr) {
            if (fieldNr < 1 || fieldNr > 40) {
                throw new ArgumentException("fieldNr must be 1...40", nameof(fieldNr));
            }
            return fieldNr == 1 ? SIMPLE : 
                New("GENERIC_" + fieldNr, 
                        Enumerable.Range(1, fieldNr).Select(i => "Field_" + i).ToArray(),
                        Enumerable.Range(1, fieldNr).Select(i => "").ToArray(), matchesOnFieldNr: true);
        }

        [NotNull]
        public readonly string Name;

        /// <summary>
        /// Only for types created with <see cref="Generic"/>, the comparison is done on the number of fields.
        /// Reason: These types are used for item creation if no type is known.
        /// </summary>
        private readonly bool _matchesOnFieldNr;

        [NotNull]
        public readonly string[] Keys;

        [NotNull]
        public readonly string[] SubKeys;

        private ItemType([NotNull] string name, [NotNull] string[] keys, [NotNull] string[] subKeys, bool matchesOnFieldNr) {
            if (keys.Length == 0) {
                throw new ArgumentException("keys.Length must be > 0", nameof(keys));
            }
            if (keys.Length != subKeys.Length) {
                throw new ArgumentException("keys.Length != subKeys.Length", nameof(subKeys));
            }
            if (subKeys.Any(subkey => !string.IsNullOrWhiteSpace(subkey) && subkey.Length < 2 && subkey[0] != '.' && subkey.Substring(1).Contains("."))) {
                throw new ArgumentException("Subkey must either be empty or .name, but not " + string.Join(" ", subKeys), nameof(subKeys));
            }

            Keys = keys.Select(s => s?.Trim()).ToArray();
            SubKeys = subKeys.Select(s => s?.Trim()).ToArray();
            Name = name;
            _matchesOnFieldNr = matchesOnFieldNr;
        }

        public bool Matches(ItemType other) {
            return _matchesOnFieldNr || other._matchesOnFieldNr ? Keys.Length == other.Keys.Length : Equals(this, other);
        }

        public static ItemType Find([NotNull] string name) {
            ItemType result;
            _allTypes.TryGetValue(name, out result);
            return result;
        }

        public static ItemType New([NotNull] string name, [NotNull, ItemNotNull] string[] keys, [NotNull, ItemNotNull] string[] subKeys, bool matchesOnFieldNr = false) {
            ItemType result;
            if (!_allTypes.TryGetValue(name, out result)) {
                _allTypes.Add(name, result = new ItemType(name, keys, subKeys, matchesOnFieldNr));
            }
            return result;
        }

        public int Length => Keys.Length;

        public bool Equals(ItemType other) {
            // ReSharper disable once UseNullPropagation - clearer for me
            if (other == null) {
                return false;
            }
            if (Keys.Length != other.Keys.Length) {
                return false;
            }
            for (int i = 0; i < Keys.Length; i++) {
                if (Keys[i] != other.Keys[i] || SubKeys[i] != other.SubKeys[i]) {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj) {
            return Equals(obj as ItemType);
        }

        [DebuggerHidden]
        public override int GetHashCode() {
            return Name.GetHashCode();
        }

        public override string ToString() {
            var result = new StringBuilder(Name);
            var sep = '(';
            for (int i = 0; i < Keys.Length; i++) {
                result.Append(sep);
                if (SubKeys[i] != "") {
                    result.Append(Keys[i]);
                    result.Append(SubKeys[i]);
                } else {
                    result.Append(Keys[i].TrimEnd('.'));
                }
                sep = ':';
            }
            result.Append(')');
            return result.ToString();
        }

        public static ItemType New(string format) {
            string[] parts = format.Split(':', ';', ' ', '(', ')');
            return New(parts[0], parts.Skip(1).Where(p => p != "").ToArray());
        }

        public static ItemType New(string name, IEnumerable<string> keysAndSubKeys) {
            string[] keys = keysAndSubKeys.Select(k => k.Split('.')[0]).ToArray();
            string[] subkeys = keysAndSubKeys.Select(k => k.Split('.').Length > 1 ? "." + k.Split('.')[1] : "").ToArray();
            return New(name, keys, subkeys);
        }

        public static void Reset() {
            _allTypes.Clear();
            ForceLoadingPredefinedTypes();
            Intern<ItemType>.Reset();
        }
    }
}