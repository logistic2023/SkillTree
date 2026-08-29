using System;
using UnityEngine;


[Serializable]
public abstract class SkillEffect
{
    public abstract void Apply();
    public abstract void Remove();
    public abstract string GetDescription();
}
