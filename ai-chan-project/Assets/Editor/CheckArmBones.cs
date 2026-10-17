#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AiChan.Editor
{
    public static class CheckArmBones
    {
        public static string CheckTPoseInfo()
        {
            var avatar = GameObject.Find("AiChan_Avatar");
            if (avatar == null) return "no_avatar";
            var anim = avatar.GetComponent<Animator>();
            if (anim == null) return "no_animator";
            var lua = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var lla = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var rua = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var rla = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            
            var cjL = lua.GetComponent<ConfigurableJoint>();
            var cjR = rua.GetComponent<ConfigurableJoint>();

            return $"LUA: axis={cjL.axis}, secAxis={cjL.secondaryAxis}, lowX={cjL.lowAngularXLimit.limit}, highX={cjL.highAngularXLimit.limit}, yLimit={cjL.angularYLimit.limit}, zLimit={cjL.angularZLimit.limit}\n" +
                   $"RUA: axis={cjR.axis}, secAxis={cjR.secondaryAxis}, lowX={cjR.lowAngularXLimit.limit}, highX={cjR.highAngularXLimit.limit}, yLimit={cjR.angularYLimit.limit}, zLimit={cjR.angularZLimit.limit}\n" +
                   $"LLA: jointAxis={lla.GetComponent<ConfigurableJoint>().axis}, secAxis={lla.GetComponent<ConfigurableJoint>().secondaryAxis}\n" +
                   $"RLA: jointAxis={rla.GetComponent<ConfigurableJoint>().axis}, secAxis={rla.GetComponent<ConfigurableJoint>().secondaryAxis}";
        }
    }
}
#endif
