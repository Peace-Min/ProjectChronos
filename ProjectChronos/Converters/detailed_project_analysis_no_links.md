# LSAM2H SMTS 프로젝트 상세 분석 보고서 (하이퍼링크 제거 버전)

본 문서는 프로젝트의 각 구성 요소와 레이어별 역할을 통합하여, 데이터 수신부터 화면 전시까지의 전체적인 메커니즘을 상세히 설명합니다.

---

## 1. LSAM2H_SMTS_GUI (실행 및 사용자 인터페이스)
*   **역할**: 시스템의 가시적인 화면과 프로그램 진입점을 제공합니다.
*   **상세 구성**:
    *   `MainWindow`: 전체 시스템의 최상위 컨테이너 및 대시보드.
    *   `Wnd / View`: 기능별 독립 팝업(Window) 및 사용자 정의 대시보드 화면(View).
*   **핵심 특징**:
    *   **View-ViewModel 1:1 매핑**: 각 XAML View는 그에 대응하는 전용 ViewModel과 1:1로 결합되어 화면 단위의 로직을 독립적으로 관리합니다.
    *   **선언적 DataContext**: View의 생성 시점에 `DataContext`를 명시적으로 할당하여 강력한 결합을 유지하며, 타입 안정성을 확보합니다.

## 2. LSAM2H.SMTSViewModel (로직 및 데이터 중개)
*   **역할**: UI 상태 관리 및 Model 데이터를 화면용 데이터로 가공하여 전달합니다.
*   **상세 구성**:
    *   `ControlStateService`: 전역 장비 타입(CC Type) 및 운용 모드 상태를 관리하는 싱글톤 서비스.
    *   `ViewToModelMediator`: 화면의 사용자 요청을 적절한 Model 레이어로 라우팅하는 중개자.
    *   `BindingModels`: 화면 바인딩에 최적화된 데이터 래퍼 객체.
*   **연동 메커니즘**:
    *   **이벤트 기반 실시간 동기화**: ViewModel 초기화 시 Model의 이벤트를 구독하여, 하위 레이어의 데이터 변화를 즉각 UI에 반영합니다.
    *  - **ViewModel별 메시지(M###) 상세 구독 매핑**:

| 메시지 ID | 메시지명 (DataModel) | 구독 ViewModel (Subscribers) | 주요 활용 목적 |
| :--- | :--- | :--- | :--- |
| **M001** | 통제소 상태 | MainWindowViewModel, StatusMsgWndViewModel | 연결 가시성 관리, 운용 모드 변경 감시 |
| **M002** | 부체계 연결 상태 | MainWindowViewModel, StatusMsgWndViewModel, OpMode_Left, OpMode_Middle, IpMode_GuidedMissile | MFR/발사대/유도탄 연결 및 빔 방사 상태 실시간 반영 |
| **M100** | I-BIT 시작/타임아웃 | StatusMsgWnd, IpMode_ComponentsTab, IpMode_GuidedMissile, 모든 IpMode_***IBITViewModel | 점검 프로세스 제어 및 시간초과(미완료) 상태 전파 |
| **M101** | 작전콘솔 I-BIT | StatusMsgWnd, IpMode_ComponentsTab, IpMode_OCCIBIT | OCC1, OCC2 점검 결과 및 상세 트리 전시 |
| **M102** | 전술작전장치 I-BIT | StatusMsgWnd, IpMode_ComponentsTab, IpMode_TCCIBIT | TCC 점검 결과 반영 |
| **M103** | 다기능콘솔 I-BIT | StatusMsgWnd, IpMode_ComponentsTab, IpMode_MCCIBIT | MCC 점검 결과 반영 |
| **M104** | 전술기록장치 I-BIT | StatusMsgWnd, IpMode_ComponentsTab, IpMode_TRUIBIT | TRU 점검 결과 반영 |
| **M105** | 전술통신장치 I-BIT | StatusMsgWnd, IpMode_ComponentsTab, IpMode_TCUIBIT | TCU 점검 결과 반영 |
| **M106** | 전술전화기 I-BIT | StatusMsgWnd, IpMode_ComponentsTab, IpMode_TTPIBIT | TTP1~TTP4 점검 결과 반영 |
| **M107** | 전원/발전기 I-BIT | StatusMsgWnd, IpMode_ComponentsTab, IpMode_PCP/GeneratorIBIT | PCP, 발전장치 점검 결과 반영 |
| **M108** | 직류전원장치 I-BIT | StatusMsgWnd, IpMode_ComponentsTab, IpMode_DPUIBIT | DPU 점검 결과 반영 |
| **M109** | 집단보호장비 I-BIT | StatusMsgWnd, IpMode_ComponentsTab, IpMode_IPUCPIBIT | 집단보호장비 점검 결과 반영 |
| **M110** | 광통신장치 I-BIT | StatusMsgWnd, IpMode_ComponentsTab, IpMode_OCUIBIT | OCU 점검 결과 반영 |
| **M111** | 다기능레이다 I-BIT | StatusMsgWnd, IpMode_MFRIBIT | MFR (천궁-II 레이다) 점검 결과 반영 |
| **M112** | 발사대 I-BIT | StatusMsgWnd, IpMode_LSIBIT | 발사대 LS1~LS6 점검 결과 반영 |
| **M113** | 유도탄 I-BIT | StatusMsgWnd, IpMode_GuidedMissile, IpMode_GuidedMissileIBIT | 각 발사대별 유도탄 상세 점검 결과 반영 |
| **M201** | 교전통제소 CBIT | StatusMsgWnd, OpMode_Left, StatusAlertMsg | 구성품(OCC, TCC 등) 상시 상태 전시 |
| **M202** | 다기능레이다 CBIT | StatusMsgWnd, OpMode_Left | 레이다 실시간 상태(방사 상태 등) 전시 |
| **M203** | 발사대 CBIT | StatusMsgWnd, OpMode_Middle | 발사대 및 유도탄 적재 현황 상시 감시 |
| **M301/2** | 기록/분석 결과 | StatusMsgWnd, MainWindowViewModel | 외부 프로세스 연동 결과 보고 및 알림 |
    *   **Messenger API**: 컴포넌트 간 통신을 위해 `Messenger.Default`를 활용하여 모듈 간 느슨한 결합을 유지합니다.

## 3. LSAM2H.SMTSModel (도메인 로직 및 외부 연동)
*   **역할**: 외부 시스템과의 통신 및 핵심 비즈니스 로직, 장비 상태 모니터링을 전담합니다.
*   **상세 구성**:
    *   **UDPCommModel**: 외부 통제소(MCC)와의 UDP 통신 핵심 모듈. 수신된 바이트 데이터를 필터링하고 역직렬화의 시작점이 됩니다.
    *   **StateModel**: 각 부체계의 연결 상태를 추적하고, 5초 내외의 메시지 미수신 시 자동으로 타임아웃을 감지하는 논리 회로를 포함합니다.
    *   **InitializeHelper**: 리플렉션을 사용하여 `ModelBaseAttribute`가 부여된 모델들을 자동으로 찾아 인스턴스를 관리합니다.
- **데이터 수신 핵심 흐름**:
    1.  `UDPCommModel`이 원시 UDP 패킷(Byte)을 수신하고, 메시지 ID를 기준으로 `MxxxDataModel`로 역직렬화합니다.
    2.  `ModelBase`의 `Send()` 메서드를 호출하여 프레임워크 내부 메시지 버스로 데이터를 송출합니다.

- **SMTSModel 주요 클래스별 상세 역할**:
    - **`UDPCommModel` (통신 핵심)**: 
        - 외부 시스템(MCC)과의 UDP 통신을 담당하는 최하단 레이어입니다.
        - 수신된 모든 바이트 데이터를 분석하여 적절한 `DataModel` 객체로 변환하고 시스템 버스로 송출하는 입구 역할을 수행합니다.
    - **`StateModel` (상태 기준점)**: 
        - `M001` 메시지를 통해 시스템 타입(ICC/ECS) 및 연결 상태(Connected/Timeout)를 관리합니다.
        - 5초 주기 하트비트 체크를 통해 장치 생존 여부를 판단하며, 타 모델들이 로직 분기에 사용하는 '현재 기준 상태'를 제공합니다.
    - **`InsCBITModel` (CBIT 모니터링)**: 
        - 상시 점검(CBIT) 메시지(M201~M203)를 처리합니다.
        - 통제소 구성품, 레이다, 발사대의 상태 정보를 수집하며, `ConcurrentDictionary`를 사용하여 멀티스레드 환경에서도 안전하게 데이터를 관리합니다.
    - **`InsIBITModel` (IBIT 관리)**: 
        - 중단 점검(IBIT) 메시지(M100~M113) 및 명령 제어를 담당하는 복잡한 로직을 포함합니다.
        - 각 장비(콘솔, 레이다, 발사대, 유도탄 등)에 대한 세부 점검 결과 수신 및 이에 따른 응답(Ack) 메시지 송출을 병행합니다.
    - **`RepModel` (보고서 및 외부 연동)**: 
        - 기록 분석 및 보고서 생성을 위해 외부 실행 파일(LSAM2H_TPU_REPORT.exe 등)을 호출하고 관리합니다.
        - `StateModel`의 현재 정보를 참조하여 실행 시 필요한 인자값을 동적으로 결정합니다.
    - **`ConfModel` / `UsrAuthModel` / `MsgModel`**: 
        - 각각 환경설정, 사용자 인증, 상태 메시지 관리를 위한 CSU(Computer Software Unit)입니다. 
        - 현재는 프레임워크 구조에 맞춘 아키텍처적 기반을 형성하고 있으며, 향후 기능 확장을 위한 로직 배치 공간으로 활용됩니다.
    - **`InitializeHelper` (자동화 도구)**: 
        - 리플렉션을 이용해 `ModelBaseAttribute`가 부여된 모든 클래스를 탐색하여 싱글톤 인스턴스를 자동으로 생성하고 초기화합니다.

## 4. LSAM2H.SMTSMessage (데이터 규격 정의)
*   **역할**: 시스템 간 통신 및 내부 데이터 전송에 사용되는 공통 메시지 규격(Protocol)을 정의합니다.
*   **상세 구성**:
    *   `Mxxx (UDP Data Model)`: 수신 상태 데이터 모델 (Status).
    *   `Sxxx (UDP Send Model)`: 송신 요청 데이터 모델 (Request).
    *   `IMarshalSerializable`: 고속 바이너리 처리를 위한 마샬링 인터페이스 및 유틸리티 제공.

## 5. FrameworkLib (핵심 인프라 및 기반 클래스)
*   **역할**: 전체 프로젝트의 생산성과 안정성을 보장하는 공통 기반 라이브러리입니다.
*   **상세 구성**:
    *   **ModelBase<T> (추상 클래스)**: 모든 모델의 부모 클래스로, `[MsgProcessMethod]` 어트리뷰트가 붙은 메서드를 리플렉션으로 탐색하여 자동 라우팅해주는 프레임워크의 핵심입니다.
    *   **MessageDistribution**: 중앙 메시지 허브입니다. `Send`된 데이터를 구독자들에게 배분(Pub/Sub)하며, `SynchronizationContext`를 활용해 스레드 안전한 데이터 처리를 지원합니다.
    *   **Comm / Util**: 시리얼/이더넷 통신 베이스, 디버그 로그 유틸리티, 컨피그 파일 리더 등.

## 6. DesignResource (공통 디자인 리소스)
*   **역할**: UI의 일관성을 유지하고 디자인 요소의 재사용성을 극대화합니다.
*   **상세 구성**:
    *   **Styles / Brushes**: WPF 공통 컨트롤(Button, Tab, GroupBox)의 테마와 색상 정보 정의.
    *   **Fonts / Images**: 시스템 지정 폰트(나눔스퀘어 등)와 아이콘, 배경 이미지 자원 관리.
    *   **AttachedBehavior**: XAML 확장 기능을 코드가 아닌 선언식으로 처리하기 위한 로직 클래스 포함.
*   **기능**: 프로젝트 전반에 풍부한 시각적 효과를 부여하고 UI 개발 생산성을 높입니다.
