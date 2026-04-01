# Unity CLI Custom Tools

[unity-cli](https://github.com/youngwoocho02/unity-cli)용 커스텀 도구 모듈. GameObject, Scene, Component, Asset, Batch 커맨드를 추가합니다.

unity-cli의 `exec`으로 할 수 있는 작업들을 **빌트인 커맨드로 래핑**하여 반복 사용을 편하게 합니다.

## 요구사항

- Unity 2022.3+
- [unity-cli](https://github.com/youngwoocho02/unity-cli) CLI 바이너리 설치
- [unity-cli-connector](https://github.com/youngwoocho02/unity-cli.git?path=unity-connector) 패키지 설치

## 설치

Unity Package Manager → Add package from git URL:

```
https://github.com/lLcrowe/UnityCLI_CustomTool.git
```

또는 `Packages/manifest.json`에 추가:

```json
"com.llcrowe.unity-cli-custom-tools": "https://github.com/lLcrowe/UnityCLI_CustomTool.git"
```

설치 후 Unity가 컴파일하면 자동으로 도구가 등록됩니다. `unity-cli list`로 확인.

## 도구 목록 (5개, 30 액션)

### gameobject (13 액션)

```bash
unity-cli gameobject --action find --name "Player"
unity-cli gameobject --action find --tag "Enemy"
unity-cli gameobject --action find --component_type Rigidbody
unity-cli gameobject --action create --name "Obj" --position "1,2,0"
unity-cli gameobject --action create_primitive --primitive_type Cube --name "Wall"
unity-cli gameobject --action set_transform --name "Wall" --position "5,0,0" --scale "2,4,1"
unity-cli gameobject --action set_active --name "Wall" --active false
unity-cli gameobject --action get_hierarchy --max_depth 3
unity-cli gameobject --action get_components --name "Player"
unity-cli gameobject --action add_component --name "Player" --component_type Rigidbody
unity-cli gameobject --action remove_component --name "Player" --component_type Rigidbody
unity-cli gameobject --action rename --name "Old" --new_name "New"
unity-cli gameobject --action duplicate --name "Template"
unity-cli gameobject --action set_parent --name "Child" --parent "Parent"
unity-cli gameobject --action destroy --name "Temp"
```

비활성 GameObject도 `--name` / `--instance_id`로 검색 가능.

### scene (7 액션)

```bash
unity-cli scene --action list
unity-cli scene --action get_active
unity-cli scene --action open --path "Assets/Scenes/Level1.unity"
unity-cli scene --action open --path "Assets/Scenes/UI.unity" --additive true
unity-cli scene --action save
unity-cli scene --action new --name "Test" --template empty
unity-cli scene --action close --path "Assets/Scenes/UI.unity"
unity-cli scene --action set_active --path "Assets/Scenes/Level1.unity"
```

### component (2 액션)

```bash
# 속성 읽기
unity-cli component --action get_properties --name "Main Camera" --component_type Camera

# 속성 쓰기 (SerializedProperty 기반)
unity-cli component --action set_property --name "Main Camera" --component_type Camera --property "field of view" --value 90
```

지원 타입: int, float, bool, string, Vector2, Vector3, Color, Enum, ObjectReference, LayerMask

### asset (7 액션)

```bash
unity-cli asset --action search --filter "t:Material"
unity-cli asset --action search --filter "t:Prefab" --folder "Assets/Prefabs"
unity-cli asset --action get_info --path "Assets/Scenes/Main.unity"
unity-cli asset --action create_folder --path "Assets/Generated"
unity-cli asset --action move --path "Assets/Old.mat" --destination "Assets/New.mat"
unity-cli asset --action duplicate --path "Assets/Template.prefab"
unity-cli asset --action delete --path "Assets/Temp"
unity-cli asset --action import --path "Assets/Textures/New.png"
```

### batch (순차 실행)

여러 커맨드를 한 번의 HTTP 요청으로 실행. Undo 그룹 통합.

```bash
unity-cli batch --params '{"commands":[
  {"command":"gameobject","params":{"action":"create_primitive","primitive_type":"Cube","name":"A"}},
  {"command":"gameobject","params":{"action":"add_component","name":"A","component_type":"Rigidbody"}}
]}'

# 실패 시 중단
unity-cli batch --params '{"commands":[...],"stop_on_error":true}'
```

## 대상 선택

GameObject는 3가지 방식으로 지정:

| 방식 | 예시 | 비활성 GO |
|------|------|----------|
| `--instance_id` | `--instance_id 12345` | ✅ |
| `--path` | `--path "Parent/Child"` | ✅ |
| `--name` | `--name "Player"` | ✅ |

## 작동 원리

이 패키지의 모든 클래스는 `[UnityCliTool]` 어트리뷰트가 붙어있습니다.
unity-cli-connector의 `ToolDiscovery`가 어셈블리를 스캔하여 자동으로 도구를 등록합니다.
별도 설정이나 초기화 코드는 필요 없습니다.

```
unity-cli CLI → HTTP → unity-cli-connector (ToolDiscovery)
                         → 이 패키지의 [UnityCliTool] 클래스 자동 발견
```

## 참고

- 모든 변경 작업은 Unity Undo 시스템에 등록됩니다 (Ctrl+Z 가능)
- 에셋 변경(create_folder, delete 등)은 도메인 리로드를 유발할 수 있습니다
- `find` 결과는 페이지네이션 지원 (`--page_size`, `--cursor`)

## License

MIT
