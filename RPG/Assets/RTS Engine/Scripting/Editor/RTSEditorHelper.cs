using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace RTSEngine
{
    public class RTSEditorHelper : Editor
    {
        public static void Navigate (ref int index, int step, int max)
        {
            if (index + step >= 0 && index + step < max)
                index += step;
        }

    }
}
