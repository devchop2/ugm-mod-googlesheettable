# Google Sheet Table

`com.chopchopgames.ugm.googlesheettable` — UGM 모듈

구글 시트 데이터를 Unity 에셋으로 캐싱하고, 런타임에 **강타입 자료구조(List / Dictionary / DictionaryOfList)** 로 파싱해 어디서든 접근하게 해주는 모듈입니다. 사용자가 정의한 `[GoogleSheetRow("table")]` 클래스를 보고 **자동으로 강타입 액세서 코드까지 생성**해줍니다.

```
[구글시트]
   │  (에디터: ChopChopGames/GoogleSheet/LoadTables)
   ▼
[TableAsset(.asset)]   ← 시트 데이터 캐시 (.asset 파일)
   │  (런타임: GoogleSheetTableManager.Load)
   ▼
[List / Dict / GroupedDict]   ← 강타입 자료구조
   │
   └─▶ GoogleSheetAccessors.{spreadsheet}.{sheet}   ← 자동 생성된 강타입 액세서
```

---

## 1. 설치

UGM에서 이 모듈을 임포트하면 `Packages/com.chopchopgames.ugm.googlesheettable/` 아래에 임베디드 패키지로 설치됩니다. 사용자 코드에서 사용하려면 asmdef의 References에 `ChopChopGames.UGM.GoogleSheetTable` 추가, 코드에서는 다음 namespace를 import:

```csharp
using ChopChopGames.UGM.GoogleSheetTable;
using ChopChopGames.UGM.GoogleSheetTable.Generated; // 자동 생성된 액세서 사용 시
```

---

## 2. 사용자 데이터 위치 (`_UserData/` 컨벤션)

이 모듈은 **재사용 가능한 코드만** 패키지에 포함하고, 사용자별 데이터는 `Assets/_UserData/` 아래에 별도로 둡니다. 다른 사용자가 이 모듈을 받아도 자기 시트로 깨끗하게 시작할 수 있게 하기 위함이에요.

```
Assets/_UserData/                                ← 사용자별 데이터 (모듈에 포함되지 않음)
├── GoogleSheetConfig.asset                      ← Config 인스턴스 (시트 ID, 시트 목록)
├── TableScripts/                                ← 사용자가 정의한 Row 타입
│   └── SampleCSV.cs
├── Tables/                                      ← LoadTables가 생성하는 TableAsset 캐시
│   └── <SpreadSheetName>/
│       └── <tableName>.asset
└── Generated/                                   ← AccessorGenerator 출력 (자동 생성)
    └── GoogleSheetAccessors.generated.cs
```

이 경로들은 모두 커스터마이즈 가능합니다:
- `Tables/` 위치: `GoogleSheetConfig.outputFolder` 필드
- `Generated/` 위치: `AccessorGenerator.OutputPath` 상수 (소스 수정 필요)

---

## 3. 빠른 시작 (5단계)

### 3.1. Config 생성

메뉴 **`ChopChopGames/GoogleSheet/Config`** 클릭. 없으면 `Assets/_UserData/GoogleSheetConfig.asset`이 자동 생성되고 인스펙터가 열립니다.

### 3.2. SpreadSheet 항목 추가

`spreadSheets` 리스트에 항목 한 개 추가:
- **name**: 논리적 이름 (예: `"sampleSpread"`). 폴더명·필드명에 쓰임
- **spreadsheetId**: 시트 URL의 `/d/` 와 `/edit` 사이 ID
- **sheets**: 이 SpreadSheet의 시트 탭 목록

### 3.3. Sheet 항목 추가

각 SpreadSheet 안에 시트 탭별로 항목 추가:
- **tableName**: 시트 탭 이름 (예: `"sample"`)
- **gid**: 시트 탭의 `gid=` 쿼리 파라미터 값
- **keyColumn**: Dictionary로 쓸 때 키 컬럼명 (List면 비워둠)
- **dataStructure**: `List` / `Dictionary` / `DictionaryOfList` 중 선택

### 3.4. Row 타입 정의

`Assets/_UserData/TableScripts/SampleCSV.cs` 참고. 시트의 컬럼 헤더와 정확히 일치하는 필드/프로퍼티를 가진 클래스에 `[GoogleSheetRow("tableName")]` 어트리뷰트 부착:

```csharp
using ChopChopGames.UGM.GoogleSheetTable;

[GoogleSheetRow("sample")]   // 시트의 tableName과 일치
public class SampleCSV
{
    public int id { get; private set; }
    public string name { get; private set; }
    public int[] assetTypes { get; private set; }   // 쉼표/파이프 구분 자동 파싱
    public float value { get; private set; }
}
```

지원 타입: `string`, `int/long/short/byte/float/double/bool`, enum, 위 타입의 `[]` 배열·`List<T>`, `Nullable<T>`. 배열은 셀에서 `,` 또는 `|`로 구분해 입력.

### 3.5. 다운로드 + 강타입 액세서 생성

메뉴 **`ChopChopGames/GoogleSheet/LoadTables`** 실행. 이 작업이 한 번에 처리하는 일:
1. 각 시트를 TSV로 다운로드해 `Assets/_UserData/Tables/<spreadsheet>/<table>.asset`으로 저장
2. Sheet 항목의 `cachedAsset` 슬롯에 자동 연결
3. Row 타입을 자동 매칭 (`[GoogleSheetRow]` 어트리뷰트 기준)
4. **`AccessorGenerator.Generate`를 자동 호출** → `Generated/GoogleSheetAccessors.generated.cs` 생성

이후 시트 데이터만 갱신할 땐 LoadTables 다시 실행. Row 타입을 추가했을 때는 메뉴 `ChopChopGames/GoogleSheet/Generate Accessors`로 액세서만 재생성도 가능.

---

## 4. 런타임에서 데이터 읽기

### 4.1. Manager 배치

씬에 빈 GameObject 만들고 `GoogleSheetTableManager` 컴포넌트 추가. 인스펙터에서:
- **Config**: 위에서 만든 Config.asset 드래그
- **Load On Awake**: 켜면 Awake에서 자동 Load (보통 켜둠)

### 4.2. 강타입 액세서로 읽기 (가장 권장)

```csharp
using ChopChopGames.UGM.GoogleSheetTable;
using ChopChopGames.UGM.GoogleSheetTable.Generated;
using UnityEngine;

public class MyGameLogic : MonoBehaviour
{
    private void Start()
    {
        var manager = GoogleSheetTableManager.Instance;
        manager.Load(success =>
        {
            if (!success) { Debug.LogError("로드 실패"); return; }

            // 자동 생성된 강타입 액세서. 컴파일 타임에 타입 체크됨.
            var sampleList = GoogleSheetAccessors.sampleSpread.sample;
            foreach (var row in sampleList)
                Debug.Log($"id={row.id}, name={row.name}");
        });
    }
}
```

`GoogleSheetAccessors`는 자동 등록 패턴(`[RuntimeInitializeOnLoadMethod]`)을 써서 **`Manager.LoadAll`이 끝날 때 자동으로 채워집니다.** 사용자가 명시적으로 init 호출할 필요 없음.

> **주의 — partial class에서 static accessor로 변경됨**: 옛 GoogleSheetTable .unitypackage는 `manager.{spreadsheet}.{sheet}` 식으로 partial class 확장을 통해 접근했는데, UPM 패키지로 바뀌면서 어셈블리 격리 때문에 static class 패턴 (`GoogleSheetAccessors.{spreadsheet}.{sheet}`) 으로 전환됐습니다. 옛 코드를 옮겨오신다면 일괄 치환하세요.

### 4.3. Manager 직접 호출 (제네릭)

액세서 자동 생성을 안 쓰거나 동적으로 접근하고 싶을 때:

```csharp
// List
IReadOnlyList<SampleCSV> list = manager.GetList<SampleCSV>();
// 또는 SpreadSheet/table 명시:
IReadOnlyList<SampleCSV> list2 = manager.GetList<SampleCSV>("sampleSpread", "sample");

// Dictionary (Config의 dataStructure가 Dictionary인 시트)
var dict = manager.GetDict<int, SampleCSV>();
manager.Find<int, SampleCSV>(42);   // 키로 한 행 찾기

// DictionaryOfList (같은 키로 여러 행 그룹핑)
var grouped = manager.GetGroupedDict<int, SampleCSV>();

// 약타입 (컬럼명·셀값 string으로 직접 접근)
Table table = manager.GetTable("sample");
TableRow row = table.GetRow("42");
string s = row["name"];
int n = row.GetInt("count", defaultValue: 0);
```

---

## 5. 시트 형식

### 5.1. 시트 공유 권한

이 모듈은 OAuth 없이 `docs.google.com/spreadsheets/.../export?format=tsv&gid=...` 로 받기 때문에 **시트가 공개돼야 합니다.**

- 시트 우상단 `공유` → `링크가 있는 모든 사용자` → **뷰어** 권한
- 사내 도메인 한정 공유도 가능. 이 경우 CI/빌드 머신이 같은 도메인 계정으로 로그인된 브라우저에서 동작해야 함.

### 5.2. 시트 데이터 형식

| 행 | 용도 |
|---|---|
| **1행** | 비워둠. 메모/타입 주석 등 자유롭게 작성해도 파서가 무시. |
| **2행** | **헤더** — 컬럼 이름. Row 클래스의 필드/프로퍼티 이름과 정확히 일치해야 함. |
| **3행~** | 데이터. 빈 행은 자동 스킵. |

배열·리스트 컬럼: 셀 안에 `1,2,3` 또는 `1|2|3` 형태로 입력. 자동으로 split.

---

## 6. dataStructure 가이드

| 값 | 언제 쓰나 | 키컬럼 |
|---|---|---|
| `List` | 순서가 있는 데이터, 중복 키 가능 (예: 퀘스트 진행 단계, 컷씬 라인) | 비워둠 |
| `Dictionary` | 한 키 = 한 행 (예: 아이템 ID → 아이템 정의) | 필수 |
| `DictionaryOfList` | 한 키 = 여러 행 묶음 (예: 캐릭터 ID → 그 캐릭터의 스킬들) | 필수 |

`keyColumn`은 시트의 **컬럼 헤더 이름과 일치**해야 하고, Row 클래스에 같은 이름의 필드가 있어야 함.

---

## 7. 메뉴 정리

| 메뉴 | 동작 |
|---|---|
| `ChopChopGames/GoogleSheet/Config` | Config.asset 열거나 신규 생성 |
| `ChopChopGames/GoogleSheet/LoadTables` | 시트 다운로드 + cachedAsset 연결 + Row 자동 매칭 + Accessors 재생성 (한 번에) |
| `ChopChopGames/GoogleSheet/Generate Accessors` | Accessors 코드만 재생성 (Row 타입을 추가/수정한 직후 유용) |

---

## 8. 트러블슈팅

**`SampleCSV` 같은 Row 타입이 자동 매칭 안 됨**
→ `[GoogleSheetRow("tableName")]` 어트리뷰트의 문자열이 시트 항목의 `tableName`과 정확히 일치하는지 확인. 대소문자 무시되지만 공백/특수문자는 일치해야 함.

**LoadTables 시 "다운로드 실패"**
→ 시트 공유 권한이 "링크 보유 사용자: 뷰어" 인지 확인. 비공개 시트는 받을 수 없음.

**Accessors 코드 생성 후 컴파일 에러**
→ Row 타입의 필드/프로퍼티 이름과 시트 헤더가 다르면, 채워지지 않은 채로 나옴(에러 아님). 진짜 에러라면 Row 타입의 namespace 충돌 가능 — `global::` prefix가 자동으로 붙도록 생성되지만, 같은 이름 클래스가 여러 namespace에 있으면 모호해질 수 있음.

**런타임에 `GoogleSheetAccessors.X.Y`가 null**
→ Manager.LoadAll이 아직 안 끝났거나, 그 시트의 `cachedAsset`이 비어 있는 경우. LoadTables를 한 번도 안 실행했으면 `cachedAsset`이 null. Manager 인스펙터에서 Load On Awake 켜져 있는지, 그리고 Load 콜백 안에서 액세서를 사용하고 있는지 확인.

**partial class 관련 컴파일 에러**
→ 옛 .unitypackage 시절의 Generated 파일이 남아 있을 수 있음. `Assets/_UserData/Generated/` 외 다른 곳에 옛 `GoogleSheetAccessors.generated.cs`가 있다면 삭제하고 LoadTables 재실행.

---

## 9. 의존성

- 외부 SDK 없음. 순수 C# + UnityEngine + UnityEditor.
- Unity 6000.0 (Unity 6) 이상 권장.
- `System.Net.Http` (Unity가 기본 제공) — 시트 다운로드용.

---

## 10. 변경 이력

[CHANGELOG.md](./CHANGELOG.md) 참조.
