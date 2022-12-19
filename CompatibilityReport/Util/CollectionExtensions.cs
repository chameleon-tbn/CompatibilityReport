using System;
using System.Collections;
using UnityEngine;

namespace CompatibilityReport.Util
{
    public static class CollectionExtensions
    {
        internal static void Enumerate(this IEnumerator enumerator) {
            try {
                while (enumerator.MoveNext()) { }
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }
    }
}
