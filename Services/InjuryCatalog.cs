using PropertyManagement.Models;

namespace PropertyManagement.Services;

public static class InjuryCatalog
{
    public static IReadOnlyList<InjuryBodyPart> Parts { get; } = Create().AsReadOnly();

    private static List<InjuryBodyPart> Create()
    {
        List<InjuryBodyPart> parts = [];
        void Add(string id, string name, string group, string side, string surface,
            double x, double y, double z, double w, double h, double d, bool overview = false, double rotation = 0)
            => parts.Add(new(id, name, group, side, surface, x, y, z, w, h, d, overview, rotation));
        Add("head", "頭頂／頭皮", "頭頸部", "中線", "雙面", 0, 7.02, 0, .37, .34, .34, true);
        Add("forehead", "前額", "頭頸部", "中線", "正面", 0, 6.96, .28, .29, .14, .12);
        Add("back-head", "後腦", "頭頸部", "中線", "背面", 0, 6.85, -.24, .30, .31, .14);
        Add("face", "臉頰／顏面", "頭頸部", "中線", "正面", 0, 6.66, .27, .29, .25, .15);
        Add("nose", "鼻部", "頭頸部", "中線", "正面", 0, 6.76, .43, .065, .10, .075);
        Add("mouth", "口腔／嘴唇", "頭頸部", "中線", "正面", 0, 6.58, .40, .12, .045, .045);
        Add("teeth", "牙齒", "頭頸部", "中線", "正面", 0, 6.59, .44, 0, 0, 0);
        Add("jaw", "下顎／下巴", "頭頸部", "中線", "正面", 0, 6.45, .22, .23, .11, .19);
        Add("neck", "頸部", "頭頸部", "中線", "雙面", 0, 6.12, 0, .22, .28, .23, true);
        Add("throat", "喉部", "頭頸部", "中線", "正面", 0, 6.10, .20, .13, .20, .065);
        Add("nape", "後頸", "頭頸部", "中線", "背面", 0, 6.10, -.20, .15, .21, .065);
        Add("sternum", "胸骨", "軀幹", "中線", "正面", 0, 5.53, .32, .10, .36, .10);
        Add("abdomen", "腹部", "軀幹", "中線", "正面", 0, 4.78, .10, .57, .43, .33, true);
        Add("pelvis", "骨盆", "軀幹", "中線", "雙面", 0, 4.10, 0, .60, .33, .34, true);
        Add("perineum", "會陰／生殖部位", "軀幹", "中線", "正面", 0, 3.84, .33, 0, 0, 0);
        Add("upper-back", "上背部", "軀幹", "中線", "背面", 0, 5.54, -.18, .68, .43, .25, true);
        Add("lower-back", "下背／腰背", "軀幹", "中線", "背面", 0, 4.78, -.20, .51, .43, .19, true);
        Add("spine", "脊椎", "軀幹", "中線", "背面", 0, 5.05, -.42, .065, .68, .045);
        Add("tailbone", "尾椎", "軀幹", "中線", "背面", 0, 3.97, -.34, .10, .15, .065);
        foreach (var (key, side, sign) in new[] { ("left", "左側", 1d), ("right", "右側", -1d) })
        {
            var prefix = side[..1];
            var arm = prefix + "上肢";
            var leg = prefix + "下肢";
            void Pair(string id, string name, string group, string surface, double x, double y, double z,
                double w, double h, double d, bool overview = false, double rotation = 0)
                => Add($"{key}-{id}", prefix + name, group, side, surface, sign * x, y, z, w, h, d, overview, sign * rotation);
            Pair("eye", "眼", "頭頸部", "正面", .14, 6.85, .40, .069, .043, .045);
            Pair("ear", "耳", "頭頸部", "雙面", .38, 6.77, 0, .075, .14, .09);
            Pair("chest", "胸部", "軀幹", "正面", .33, 5.52, .14, .40, .46, .32, true);
            Pair("ribs", "肋部", "軀幹", "雙面", .55, 5.10, 0, .18, .35, .29);
            Pair("waist", "側腰", "軀幹", "雙面", .49, 4.46, 0, .14, .24, .26);
            Pair("groin", "腹股溝", "軀幹", "正面", .25, 3.94, .24, .16, .15, .16);
            Pair("buttock", "臀部", "軀幹", "背面", .28, 4.02, -.17, .32, .30, .27);
            Pair("shoulder", "肩部", arm, "雙面", .87, 5.66, 0, .27, .28, .29, true);
            Pair("clavicle", "鎖骨", arm, "正面", .41, 5.94, .17, .39, .065, .13);
            Pair("scapula", "肩胛", arm, "背面", .40, 5.62, -.34, .23, .28, .08);
            Pair("upper-arm", "上臂", arm, "雙面", 1.05, 5.12, 0, .19, .47, .20, true, .20);
            Pair("elbow", "手肘", arm, "雙面", 1.18, 4.61, 0, .19, .18, .20, true);
            Pair("forearm", "前臂", arm, "雙面", 1.33, 4.10, 0, .155, .43, .17, false, .20);
            Pair("wrist", "手腕", arm, "雙面", 1.44, 3.61, 0, .13, .14, .13, true);
            Pair("palm", "手掌", arm, "正面", 1.52, 3.34, .035, .18, .25, .11, true);
            Pair("hand-back", "手背", arm, "背面", 1.52, 3.34, -.075, .17, .23, .045);
            string[] fingers = ["拇指", "食指", "中指", "無名指", "小指"];
            double[] fx = [1.28, 1.42, 1.53, 1.64, 1.74];
            double[] fy = [3.15, 2.97, 2.91, 2.95, 3.03];
            for (var i = 0; i < fingers.Length; i++)
                Pair($"finger-{i + 1}", fingers[i], arm, "雙面", fx[i], fy[i], .04, .046, i == 0 ? .13 : .17, .058, false, i == 0 ? -.45 : 0);
            Pair("hip", "髖部", leg, "雙面", .52, 3.90, 0, .20, .24, .27);
            Pair("thigh", "大腿", leg, "雙面", .33, 3.11, 0, .275, .61, .30, true);
            Pair("knee", "膝部", leg, "雙面", .33, 2.43, .035, .205, .20, .225, true);
            Pair("shin", "小腿前側", leg, "正面", .33, 1.67, .07, .17, .57, .18, true);
            Pair("calf", "小腿後側", leg, "背面", .33, 1.67, -.09, .20, .56, .19, true);
            Pair("ankle", "腳踝", leg, "雙面", .33, .91, 0, .145, .21, .16, true);
            Pair("heel", "腳跟", leg, "背面", .33, .55, -.13, .16, .16, .19);
            Pair("foot", "腳背", leg, "正面", .33, .55, .17, .20, .15, .33, true);
            Pair("sole", "腳底", leg, "雙面", .33, .40, .17, .19, .045, .33);
            for (var i = 0; i < 5; i++)
                Pair($"toe-{i + 1}", i == 0 ? "大腳趾" : $"第 {i + 1} 腳趾", leg, "雙面",
                    .19 + i * .078, .50, .51 - i * .022, .046 - i * .004, .055, .09 - i * .009);
        }
        Add("chest-internal", "胸腔／內臟", "全身與其他", "中線", "內部", 0, 5.4, 0, 0, 0, 0);
        Add("abdomen-internal", "腹腔／內臟", "全身與其他", "中線", "內部", 0, 4.7, 0, 0, 0, 0);
        Add("whole-body", "全身／多處部位", "全身與其他", "全身", "其他", 0, 4, 0, 0, 0, 0);
        Add("body-systems", "全身性／身體系統", "全身與其他", "全身", "其他", 0, 4, 0, 0, 0, 0);
        Add("mental", "心理／精神", "全身與其他", "全身", "其他", 0, 7, 0, 0, 0, 0);
        Add("other", "其他／未明部位", "全身與其他", "全身", "其他", 0, 4, 0, 0, 0, 0);
        return parts;
    }
}
