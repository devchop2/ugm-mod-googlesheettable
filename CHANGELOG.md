# Changelog

## [0.1.1] - 2026-05-08
### Added
- **개별 테이블 reload 기능**
  - `GoogleSheetConfig` 인스펙터의 각 SheetEntry foldout 우측에 `⟳ Reload` 버튼 — 해당 시트 한 개만 다시 다운로드
  - `TableAsset` 인스펙터 상단의 `⟳ Reload from Sheet` 버튼 — config 의 매칭 entry 자동 검색 후 단일 다운로드
  - `GoogleSheetDownloader.DownloadOne(config, spreadSheet, entry)` API 추가
- **헤더 행 인덱스 설정 가능 (per-sheet)**
  - `SheetEntry.headerRow` 필드 추가 (1-based, 기본값 2 — 행 1 은 노트, 행 2 가 헤더, 행 3 부터 데이터)
  - 첫 행이 바로 헤더인 시트는 `headerRow = 1` 로 설정. 더 큰 값(예: 5)이면 1~4행은 무시되고 5행이 헤더, 6행부터 데이터.
  - `TsvParser.Parse(name, tsv, keyColumn, headerRow)` 시그니처 확장 (default 2 → backward compatible).
- **cachedAsset 자동 복구 도구** (`Editor/ConfigSyncer.cs` 신규)
  - `ConfigSyncer.RepairAssetLinks(config)` — outputFolder 의 .asset 파일들을 tableName 매칭으로 cachedAsset 자동 연결
  - 메뉴 `ChopChopGames > GoogleSheet > Repair cachedAsset Links`
  - **`LoadTables` 실행 직전에 자동으로 한 번 호출** (외부 편집/머지로 끊어진 참조 자동 회복)
- **Sync Config From Seed JSON 기능**
  - 외부 JSON 정의로부터 `config.spreadSheets` 전체를 재구성 (기존 cachedAsset 은 가능한 한 보존)
  - 메뉴 `ChopChopGames > GoogleSheet > Sync Config From Seed JSON…`
  - JSON 형식: `[{name, spreadsheetId, sheets:[[tableName, gid, dataStructure, keyColumn, headerRow], ...]}, ...]`
  - 외부 도구로 config 가 잘리거나 entry 가 사라진 상황에서 복구용
- **Config 인스펙터 검색/정렬 기능**
  - 상단 검색 바 — spreadsheet 이름과 sheet의 tableName 양쪽에 대해 substring 매칭
  - `Sort SpreadSheets A→Z` 버튼 — 최상위 SpreadSheet 목록 알파벳 정렬
  - `Sort Sheets A→Z` 버튼 — 각 SpreadSheet 안의 sheet 목록을 tableName 기준 정렬
  - 큰 config 에서 빠르게 항목 찾기 가능

### Changed
- **다운로드 실패 로그 개선**
  - 모든 실패 케이스가 `[GoogleSheet] 구글로부터 다운로드 실패 — '{spreadsheet} / {tableName}'` 통일된 형식으로 출력
  - 실패 사유 / URL / 권장 해결법 (공유 권한 변경) 함께 표시
- **silent 실패 감지 추가**
  - 비공개 시트는 HTTP 200 OK 와 함께 로그인 HTML 을 반환해서 try/catch 에 안 잡히고 garbage TableAsset 이 만들어지던 문제 해결
  - 응답 본문 시작이 `<!DOCTYPE`/`<html` 인지 검사해 명시적 실패로 처리

## [0.1.0] - 2026-05-03
### Added
- 초기 UGM 모듈 릴리스. 기존 Assets/GoogleSheetTable .unitypackage에서 UPM 패키지로 변환.
- Runtime: GoogleSheetTableManager, TableAsset, Table, TableRow, TsvParser, TypedTableParser, GoogleSheetConfig, GoogleSheetLoader, GoogleSheetRowAttribute
- Editor: AccessorGenerator, GoogleSheetDownloader, GoogleSheetMenu, RowTypeResolver, SheetEntryDrawer, TableAssetEditor, GoogleSheetConfigEditor

### Changed
- namespace: `GoogleSheetTable` → `ChopChopGames.UGM.GoogleSheetTable`
- Editor namespace: `GoogleSheetTable.EditorTools` → `ChopChopGames.UGM.GoogleSheetTable.EditorTools` (asmdef 이름은 `ChopChopGames.UGM.GoogleSheetTable.Editor`로 두지만, C# namespace는 `.EditorTools`로 둠 — `UnityEditor.Editor` 타입과의 CS0118 충돌 회피)
- **API 변경**: 강타입 액세서를 `manager.{spreadsheet}.{sheet}` (partial class 확장) 에서 `GoogleSheetAccessors.{spreadsheet}.{sheet}` (별도 static class) 로 이전. UPM 어셈블리 격리 때문에 partial class를 다른 어셈블리로 갈라놓을 수 없음.
- AccessorGenerator의 출력 위치 기본값이 `Assets/_UserData/Generated/`로 변경 (Config의 `outputFolder`로 커스터마이즈 가능).
- GoogleSheetConfig.outputFolder 기본값이 `Assets/_UserData/Tables`로 변경.
