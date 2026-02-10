using System;
using System.Collections.Generic;
using UnityEngine;

namespace CustomArmorStats
{
    public class ArmorData
    {
        public string name;

        public float armor;
        public float armorPerLevel;
        public float movementModifier;
        public int setSize;
        public string setName;
        public List<string> damageModifiers = new List<string>();
        public Dictionary<string, object> equipStatusEffects;
        public Dictionary<string, object> setStatusEffects;
    }
}