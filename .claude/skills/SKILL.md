# unity-cli — Unity Editor CLI 제어

> MCP 없이 Unity를 터미널에서 직접 제어. stateless HTTP, 멀티 클라이언트 동시접근 가능.
> 트리거: unity-cli, 게임오브젝트 CLI, 씬 CLI, 에셋 CLI, exec, 컴포넌트 속성, 배치실행

## CLI 경로
```
/c/Users/llcro/AppData/Local/unity-cli/unity-cli.exe
```

## 빠른 참조

### 상태 확인
```bash
unity-cli status                    # 연결 확인
unity-cli console --type error      # 에러 로그
unity-cli editor refresh --compile  # 컴파일 + 대기
```

### GameObject (13개 액션)
```bash
unity-cli gameobject --action find --name "Player"
unity-cli gameobject --action find --tag "Enemy"
unity-cli gameobject --action find --component_type Rigidbody
unity-cli gameobject --action create --name "Obj" --position "1,2,0"
unity-cli gameobject --action create_primitive --primitive_type Cube --name "Wall"
unity-cli gameobject --action set_transform --name "Wall" --position "5,0,0" --rotation "0,45,0" --scale "2,2,2"
unity-cli gameobject --action set_active --name "Wall" --active false
unity-cli gameobject --action get_hierarchy --max_depth 3
unity-cli gameobject --action get_components --name "Player"
unity-cli gameobject --action add_component --name "Player" --component_type Rigidbody
unity-cli gameobject --action remove_component --name "Player" --component_type Rigidbody
unity-cli gameobject --action rename --name "Old" --new_name "New"
unity-cli gameobject --action duplicate --name "Template"
unity-cli gameobject --action set_parent --name "Child" --parent "Parent"
unity-cli gameobject --action destroy --name "Temp"
unity-cli gameobject --action destroy --instance_id -1234   # 비활성 GO도 가능
```

### Scene (7개 액션)
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

### Component (2개 액션)
```bash
unity-cli component --action get_properties --name "Main Camera" --component_type Camera
unity-cli component --action set_property --name "Main Camera" --component_type Camera --property "field of view" --value 90
unity-cli component --action set_property --name "Obj" --component_type Transform --property m_LocalPosition --value "0,1,0"
```

### Asset (7개 액션)
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

### Batch (순차 실행, 1 Undo 그룹)
```bash
unity-cli batch --params '{"commands":[{"command":"gameobject","params":{"action":"create","name":"A"}},{"command":"gameobject","params":{"action":"add_component","name":"A","component_type":"Rigidbody"}}]}'
```

### exec (C# 직접 실행 — 가장 강력)
```bash
# 단순 조회
unity-cli exec "return Application.dataPath;"

# 복잡한 작업은 stdin 파이프
echo 'return Physics2D.gravity.ToString();' | unity-cli exec

# 멀티라인
cat <<'EOF' | unity-cli exec
var go = GameObject.Find("Player");
var rb = go.GetComponent<Rigidbody2D>();
rb.mass = 5f;
return $"mass: {rb.mass}";
EOF

# SerializedProperty로 참조 연결 (Inspector 배열 등)
cat <<'EOF' | unity-cli exec
var comp = GameObject.Find("Mover").GetComponent<MyComponent>();
var so = new UnityEditor.SerializedObject(comp);
var prop = so.FindProperty("targets");
prop.arraySize = 3;
for (int i = 0; i < 3; i++)
    prop.GetArrayElementAtIndex(i).objectReferenceValue = GameObject.Find($"Target_{i}").transform;
so.ApplyModifiedProperties();
return "done";
EOF
```

### 기타
```bash
unity-cli editor play --wait       # Play 모드 진입 + 대기
unity-cli editor stop              # Play 모드 종료
unity-cli screenshot --view game   # 게임뷰 캡처
unity-cli profiler --action hierarchy --depth 3
unity-cli reserialize Assets/Prefabs/Player.prefab  # YAML 정규화
unity-cli test --mode EditMode --filter MyTest
unity-cli menu "File/Save Project"
unity-cli list                     # 전체 도구 목록
```

## 주의사항
- **에셋 변경**(create_folder, delete 등) 시 응답 유실 가능 (도메인 리로드). 동작은 정상
- **비활성 GO**: name/path로 검색 가능 (Resources.FindObjectsOfTypeAll 폴백)
- **batch**: `--commands`가 아닌 `--params '{"commands":[...]}'` 형식 사용
- **exec 타임아웃**: 복잡한 코드는 `--timeout 60000` 추가
- **프로젝트 선택**: 여러 Unity 열려있으면 `--project 프로젝트명` 지정
