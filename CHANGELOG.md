# Changelog

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
