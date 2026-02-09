# ProjectChronos

## 프로젝트 개요

**ProjectChronos**는 시뮬레이션 재생 및 분석을 위한 WPF 기반 타임라인 컨트롤 라이브러리입니다. 
YouTube와 같은 현대적인 미디어 플레이어의 UX를 차용하여, 시뮬레이션 데이터를 직관적으로 탐색하고 분석할 수 있는 강력한 도구를 제공합니다.

## 주요 기능

### 🎬 시뮬레이션 재생 컨트롤
- **재생/일시정지**: 시뮬레이션을 실시간으로 재생하거나 일시정지
- **가변 배속**: 0.1x ~ 10x까지 자유로운 재생 속도 조절
- **정밀한 시간 탐색**: 슬라이더를 통한 직관적인 시간 이동

### 📍 이벤트 마커 시스템
- **시각적 마커**: 타임라인 상에 중요 이벤트를 시각적으로 표시
- **우선순위 기반 스타일링**: 
  - 🔴 **Critical** (높음): 빨간색 마커
  - 🟠 **Warning** (중간): 주황색 마커
  - 🔵 **Info** (낮음): 파란색 마커
- **툴팁 지원**: 마커에 마우스를 올리면 이벤트 설명 표시
- **이벤트 점프**: 이전/다음 이벤트로 즉시 이동

### ⚡ 스텝 네비게이션
- **커스텀 간격 설정**: 사용자 정의 시간 간격으로 이동
- **키보드 단축키**: 
  - `Space`: 재생/일시정지
  - `←/→`: 이전/다음 스텝
  - `Ctrl+←/→`: 이전/다음 이벤트

### 🎨 현대적인 UI/UX
- **다크 테마**: 눈의 피로를 줄이는 세련된 다크 모드
- **부드러운 애니메이션**: 60fps VSync 기반 렌더링
- **YouTube 스타일 레이어링**: Track(Z=1) < Marker(Z=2) < Thumb(Z=3)

## 기술 스택

- **프레임워크**: .NET WPF (Windows Presentation Foundation)
- **아키텍처**: MVVM (Model-View-ViewModel)
- **렌더링**: CompositionTarget.Rendering (VSync)
- **시간 정밀도**: Stopwatch 기반 고정밀 타이머

## 프로젝트 구조

```
ProjectChronos/
├── Models/
│   └── SimulationModels.cs          # 데이터 모델 (EventPriority, SimulationEventMarker)
├── ViewModels/
│   └── SimulationReplayViewModel.cs # 재생 로직 및 상태 관리
├── Views/
│   ├── SimulationReplayView.xaml    # UI 레이아웃 및 스타일
│   └── SimulationReplayView.xaml.cs # 코드 비하인드 (CompositionTarget 연결)
├── Converters/
│   └── PriorityToTagConverter.cs    # 우선순위 → 스타일 태그 변환
└── Core/
    └── ViewModelBase.cs             # MVVM 기반 클래스

```

## 사용 방법

### 1. 기본 사용 예제

```csharp
// ViewModel 초기화
var viewModel = new SimulationReplayViewModel();

// 시뮬레이션 데이터 설정
var events = new List<SimulationEventMarker>
{
    new SimulationEventMarker(2.5, EventPriority.High, "시스템 시작"),
    new SimulationEventMarker(5.0, EventPriority.Medium, "데이터 로드 완료"),
    new SimulationEventMarker(8.3, EventPriority.Low, "정상 작동 중")
};

viewModel.Initialize(totalDuration: 10.0, events: events);

// View에 DataContext 설정
simulationReplayView.DataContext = viewModel;
```

### 2. 이벤트 감지

```csharp
// CurrentEvent 속성을 통해 현재 활성 이벤트 확인
viewModel.PropertyChanged += (s, e) =>
{
    if (e.PropertyName == nameof(viewModel.CurrentEvent))
    {
        var currentEvent = viewModel.CurrentEvent;
        if (currentEvent != null)
        {
            Console.WriteLine($"이벤트 감지: {currentEvent.Description}");
        }
    }
};
```

## 핵심 설계 원칙

### Z-Index 레이어링
타임라인의 시각적 계층 구조는 다음과 같이 설계되었습니다:

1. **Track (Z=1)**: 배경 트랙 (시각적 가이드)
2. **Markers (Z=2)**: 이벤트 마커 (상호작용 가능)
3. **Slider (Z=3)**: 썸(Thumb) 컨트롤 (최상위 조작)

이 구조는 YouTube 타임라인의 UX를 모방하여, 썸이 마커 위를 지나가면서도 마커의 툴팁이 정상적으로 작동하도록 설계되었습니다.

### 투명 배경 히트 테스트
```xaml
<Slider Background="{x:Null}" .../>
```
슬라이더의 빈 영역을 투명하게 설정하여, 마우스 이벤트가 하위 레이어(마커)로 전달되도록 구현했습니다.

## 성능 최적화

- **VSync 동기화**: `CompositionTarget.Rendering` 이벤트를 통한 60fps 렌더링
- **델타 타임 계산**: `Stopwatch`를 사용한 정밀한 시간 측정
- **시간 점프 방지**: 시스템 렉 발생 시 최대 0.1초로 dt 제한

## 개발 환경

- **IDE**: Visual Studio 2022
- **언어**: C# 10.0
- **타겟 프레임워크**: .NET 6.0 이상
- **OS**: Windows 10/11

## 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다.

## 기여

버그 리포트, 기능 제안, Pull Request는 언제나 환영합니다!

## 작성자

**Peace-Min**  
GitHub: [@Peace-Min](https://github.com/Peace-Min)

---

**Made with ❤️ for Simulation Analysis**
