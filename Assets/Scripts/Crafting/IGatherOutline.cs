using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 채집할 수 있는 월드 오브젝트(돌/나무/풀/광맥 등). 이름 표시는 하지 않고,
    /// 지금 캘 수 있는 상태일 때만 가까이 가면 윤곽선이 굵게 나타난다 (GatherOutlineHighlighter).
    /// </summary>
    public interface IGatherOutline
    {
        /// <summary>지금 이 대상을 캘 수 있는가 (도구 등급/장비 조건을 맞췄고 고갈/채집 완료가 아님).</summary>
        bool CanGatherNow { get; }

        /// <summary>윤곽선을 그릴 모델의 루트 (이 아래의 메쉬에 윤곽선이 생긴다).</summary>
        GameObject OutlineRoot { get; }
    }
}
