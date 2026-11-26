using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/LastTruck/Animations/Mixamo 아래 FBX(Mixamo에서 받은 동작)를 자동으로 맞게 가져온다.
///  - 사람형(Humanoid) 뼈대로 읽어서 Synty 좀비 / KayKit 캐릭터에 그대로 옮겨 쓴다.
///  - 재질/카메라/조명은 가져오지 않는다 (동작만 쓴다).
///  - 클립 이름을 파일 이름으로 바꾸고(Mixamo는 전부 "mixamo.com"), 걷기/달리기/대기는 반복.
///  - 제자리 동작: 앞으로 나가는 이동은 버리고(이동은 NavMesh/캐릭터 컨트롤러가 한다), 방향/높이는 동작에 굽는다.
/// </summary>
public class MixamoAnimationImporter : AssetPostprocessor
{
    private const string Folder = "Assets/LastTruck/Animations/Mixamo/";

    private bool IsMixamo => assetPath.Replace('\\', '/').StartsWith(Folder) && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

    private static bool IsLoop(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("idle") || n.Contains("walk") || n.Contains("run") || n.Contains("crawl") || n.Contains("biting");
    }

    private void OnPreprocessModel()
    {
        if (!IsMixamo) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importAnimation = true;
        importer.resampleCurves = true;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
    }

    private void OnPreprocessAnimation()
    {
        if (!IsMixamo) return;
        var importer = (ModelImporter)assetImporter;
        string name = Path.GetFileNameWithoutExtension(assetPath);
        // 매번 기본 클립에서 다시 만든다 (이 폴더의 설정은 전부 이 스크립트가 정한다)
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        bool loop = IsLoop(name);
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation c = clips[i];
            c.name = clips.Length == 1 ? name : name + "_" + i;
            c.loopTime = loop;
            c.loopPose = false;
            // 방향: 동작에 굽는다 (몸이 돌아가지 않게, 원래 방향 기준)
            c.lockRootRotation = true;
            c.keepOriginalOrientation = true;
            // 높이: 동작에 굽는다. 쓰러지기/일어나기는 원래 높이(몸이 실제로 내려간다), 나머지는 발 기준
            c.lockRootHeightY = true;
            c.keepOriginalPositionY = true;
            c.heightFromFeet = false;
            // 앞뒤 이동: 기본은 제자리(굽지 않음 → 루트 모션으로 빠지고 적용하지 않는다. 밀려나는 건 코드가 한다).
            // 죽는 동작만 몸이 실제로 넘어간 자리에 남게 굽는다 (그대로 사라지므로 되돌아오는 미끄러짐이 없다).
            bool death = name.IndexOf("Death", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Dying", System.StringComparison.OrdinalIgnoreCase) >= 0;
            c.lockRootPositionXZ = death;
            c.keepOriginalPositionXZ = death;
            clips[i] = c;
        }

        // 뒤로 넘어지기는 "세게 맞아 넘어졌다가 일어나기"에도 쓴다 → 제자리 버전(Knockdown_Fall)을 하나 더 만든다
        // (일어나기 동작도 제자리라 이어 붙여도 몸이 튀지 않는다)
        if (name == "Falling_Back_Death" && clips.Length == 1)
        {
            ModelImporterClipAnimation k = clips[0];
            var knock = new ModelImporterClipAnimation
            {
                name = "Knockdown_Fall",
                takeName = k.takeName,
                firstFrame = k.firstFrame,
                lastFrame = k.lastFrame,
                loopTime = false,
                lockRootRotation = true, keepOriginalOrientation = true,
                lockRootHeightY = true, keepOriginalPositionY = true, heightFromFeet = false,
                lockRootPositionXZ = false, keepOriginalPositionXZ = false,
            };
            clips = new[] { clips[0], knock };
        }
        importer.clipAnimations = clips;
    }
}
