using ChopChopGames.UGM.GoogleSheetTable;
using ChopChopGames.UGM.GoogleSheetTable.Generated;
using UnityEngine;

public class SampleTableLogger : MonoBehaviour
{
    private void Start()
    {
        var manager = GoogleSheetTableManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[SampleLogger] GoogleSheetTableManager 가 씬에 없습니다.");
            return;
        }

        manager.Load(success =>
        {
            if (!success)
            {
                Debug.LogError("[SampleLogger] 로드 실패.");
                return;
            }

            var list = GoogleSheetAccessors.sampleSpread.sample;
            if (list == null)
            {
                Debug.LogError("[SampleLogger] SampleCSV 리스트를 가져오지 못했습니다.");
                return;
            }

            Debug.Log($"[SampleLogger] sample 리스트 개수: {list.Count}");
            foreach (var s in list)
                Debug.Log($"  id={s.id}, name={s.name}");
        });
    }
}
