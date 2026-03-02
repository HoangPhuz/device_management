# Tài liệu Hệ thống Quản lý Thiết bị (Device Management System)

Tài liệu mô tả toàn bộ kiến trúc, chức năng và triển khai của ứng dụng **Device Management** — ứng dụng desktop WinUI 3 quản lý danh sách model thiết bị trong kho, mượn/trả thiết bị và đồng bộ đa instance.

---

## 1. Giới thiệu (Introduction)

### Tài liệu (Document)

- **Document Purpose (Mục đích tài liệu)**: Tài liệu này mô tả tổng quan, yêu cầu chức năng và phi chức năng, kiến trúc, cấu trúc mã nguồn, mô hình dữ liệu, luồng nghiệp vụ, cơ chế đồng bộ đa instance, cấu hình và cách build/chạy của hệ thống **Device Management**. Tài liệu được sử dụng làm chuẩn tham chiếu cho phát triển, bảo trì và onboard thành viên mới.
- **Scope (Phạm vi)**: Nội dung tập trung vào ứng dụng Device Management chạy dưới dạng desktop client-only trên Windows, bao gồm: mô tả use case (functional requirements), non-functional requirements, kiến trúc Clean Architecture + MVVM, các thành phần Domain/Data/Presentation/Infrastructure, cơ chế đồng bộ real-time giữa nhiều instance, cấu hình lưu trữ local và hướng dẫn triển khai. Tài liệu không đi sâu vào thiết kế chi tiết từng lớp hay test case mức thấp.
- **Intended Audience (Đối tượng sử dụng)**: Developer, QA, Technical Lead, người vận hành/bảo trì hệ thống có kiến thức cơ bản về .NET, WinUI 3, SQLite và các khái niệm Clean Architecture, MVVM.

### Phần mềm (Software)

- **Software Purpose (Mục đích phần mềm)**: Ứng dụng Device Management cung cấp khả năng quản lý danh sách model thiết bị trong kho, hỗ trợ người dùng tìm kiếm, lọc, sắp xếp, mượn và trả thiết bị, đồng thời đảm bảo các instance đang chạy luôn hiển thị trạng thái kho nhất quán thông qua cơ chế đồng bộ real-time.
- **Scope (Phạm vi phần mềm)**: Phần mềm bao gồm hai màn hình chính `Request Device` (xem danh sách model trong kho và mượn thiết bị) và `My Device` (xem và trả thiết bị đã mượn), hỗ trợ filter/sort/pagination trên tập dữ liệu lớn (khoảng 1.000.000 bản ghi), lưu trữ dữ liệu trên SQLite local và đồng bộ nhiều instance qua UDP broadcast. Ngoài phạm vi: không cung cấp server trung tâm, không có authentication/authorization, không xử lý báo cáo nâng cao hay tích hợp hệ thống bên ngoài.
- **Intended Audience (Đối tượng người dùng phần mềm)**: Người dùng nội bộ tổ chức cần quản lý và mượn/trả thiết bị (ví dụ nhân viên kho, phòng lab, kỹ sư kiểm thử), làm việc trên máy trạm Windows và không yêu cầu kiến thức kỹ thuật sâu về hệ thống.

---

## Mục lục

1. [Giới thiệu (Introduction)](#1-giới-thiệu-introduction)
2. [Tổng quan](#2-tổng-quan)
3. [Yêu cầu chức năng](#3-yêu-cầu-chức-năng)
4. [Yêu cầu phi chức năng (Non-functional Requirements)](#4-yêu-cầu-phi-chức-năng-non-functional-requirements)
5. [Kiến trúc hệ thống](#5-kiến-trúc-hệ-thống)
6. [Cấu trúc dự án](#6-cấu-trúc-dự-án)
7. [Công nghệ và thư viện](#7-công-nghệ-và-thư-viện)
8. [Mô hình dữ liệu](#8-mô-hình-dữ-liệu)
9. [Luồng nghiệp vụ chính](#9-luồng-nghiệp-vụ-chính)
10. [Đa instance và đồng bộ](#10-đa-instance-và-đồng-bộ)
11. [Cấu hình và lưu trữ](#11-cấu-hình-và-lưu-trữ)
12. [Build và chạy ứng dụng](#12-build-và-chạy-ứng-dụng)
13. [Sequence Diagrams (PlantUML)](#13-sequence-diagrams-plantuml)

---

## 2. Tổng quan

- **Mục đích**: Cho phép người dùng xem danh sách model thiết bị trong kho, lọc/sắp xếp, mượn thiết bị và xem/trả thiết bị đã mượn. Hệ thống hỗ trợ nhiều instance chạy đồng thời với đồng bộ dữ liệu real-time và phân tách danh sách "My Device" theo từng instance.
- **Nền tảng**: Windows (WinUI 3, Windows App SDK), .NET 8.
- **Giao diện**: Hai trang chính — **Request Device** (danh sách model kho) và **My Device** (thiết bị đã mượn), điều hướng bằng `NavigationView`, phân trang tối đa 5 nút trang.

---

## 3. Yêu cầu chức năng

| UC | Mô tả |
|----|--------|
| **UC_01** | Xem danh sách Model trong kho — phân trang, truy vấn từ SQLite. |
| **UC_02** | Lọc Model theo Model, Manufacturer, Category, SubCategory (kết hợp filter). |
| **UC_03** | Sắp xếp Model theo cột (ASC/DESC). |
| **UC_04** | Mượn Model — chọn số lượng qua ContentDialog, cập nhật Available/Reserved, đồng bộ instance khác. |
| **UC_05** | Trả Model — chọn một hoặc nhiều thiết bị (checkbox), xác nhận, cập nhật DB và đồng bộ. |
| **UC_06** | Đồng bộ trạng thái giữa các instance — UDP broadcast khi có thay đổi; instance khác nhận và reload dữ liệu. |

**Ràng buộc hiệu năng**: Các thao tác filter/sort/pagination hoạt động trong thời gian ≤ 1 giây (với bộ dữ liệu mẫu 1.000.000 model, nhờ index SQLite và WAL).

---

## 4. Yêu cầu phi chức năng (Non-functional Requirements)

- **Performance**: Các thao tác filter/sort/pagination trên danh sách model phải hoàn thành trong thời gian ≤ 1 giây với bộ dữ liệu mẫu khoảng 1.000.000 bản ghi, dựa trên việc tối ưu index SQLite, sử dụng WAL mode và debounce 300ms cho filter text.
- **Reliability / Availability**: Ứng dụng chạy theo mô hình client-only, dữ liệu lưu trữ trên SQLite local; cơ chế `SyncService` sử dụng UDP broadcast giúp các instance đang hoạt động nhận biết thay đổi dữ liệu và tự reload để giữ trạng thái hiển thị nhất quán.
- **Compatibility**: Hệ thống chạy trên Windows 10/11 (build 19041+), sử dụng .NET 8 (`net8.0-windows10.0.19041.0`), hỗ trợ các kiến trúc x86, x64, ARM64 và phụ thuộc vào WinUI 3, Windows App SDK.
- **Usability**: Giao diện người dùng chia thành hai trang chính (`Request Device`, `My Device`) với `NavigationView`, DataGrid hỗ trợ filter/sort trực quan, phân trang tối đa 5 nút (sliding window) và các `ContentDialog` rõ ràng cho thao tác mượn/trả và xác nhận.
- **Maintainability / Scalability**: Kiến trúc theo Clean Architecture kết hợp MVVM, tách biệt các layer Domain, Data, Presentation, Infrastructure và sử dụng dependency injection, giúp dễ mở rộng thêm use case, repository hoặc thay thế `ISqliteDataSource` mà không ảnh hưởng đến phần còn lại.
- **Security**: Hệ thống không triển khai authentication/authorization hay encryption; dữ liệu (SQLite DB, file cấu hình slot) nằm trong `%LocalAppData%` của từng user trên máy cục bộ, cần được bảo vệ bởi chính sách bảo mật của hệ điều hành và môi trường vận hành.

---

## 5. Kiến trúc hệ thống

Hệ thống áp dụng **Clean Architecture** với các lớp:

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation (WinUI 3)                   │
│  MainWindow, RequestDevicePage, MyDevicePage, ViewModels     │
└───────────────────────────────┬─────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────┐
│                      Domain                                  │
│  Entities, ValueObjects, Interfaces, Use Cases               │
└───────────────────────────────┬─────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────┐
│                        Data                                  │
│  Repositories, SqliteDataSource, Implementations             │
└───────────────────────────────┬─────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────┐
│                   Infrastructure                             │
│  SyncService (UDP), InstanceSlotManager (persist InstanceId)  │
└─────────────────────────────────────────────────────────────┘
```

- **Presentation**: MVVM (CommunityToolkit.Mvvm), ViewModels gọi Use Cases, không phụ thuộc trực tiếp vào Data/Infrastructure ngoài DI.
- **Domain**: Entities (`DeviceModel`, `BorrowedDevice`), Value Objects (`PagedResult<T>`, `QueryParameters`), Interfaces repository, Use Cases (single responsibility).
- **Data**: SQLite qua `ISqliteDataSource`, Repository triển khai filter/sort/pagination bằng SQL động và index.
- **Infrastructure**: Đồng bộ đa instance (`SyncService`), quản lý slot/`InstanceId` (`InstanceSlotManager`).

### Presentation layer (Presentation Module)

- **Vai trò chính**: Là lớp giao tiếp với người dùng, hiển thị dữ liệu và nhận thao tác (click, filter, sort, mượn/trả). Presentation không chứa nghiệp vụ lõi mà chỉ điều phối và binding dữ liệu.
- **Thành phần tiêu biểu**:
  - `MainWindow` và `NavigationView`: khung điều hướng giữa hai trang `RequestDevicePage` và `MyDevicePage`.
  - **Views**: `RequestDevicePage`, `MyDevicePage` hiển thị DataGrid, filter, pagination và các nút lệnh (Borrow, Return Selected).
  - **ViewModels**: `RequestDeviceViewModel`, `MyDeviceViewModel` (theo MVVM/`ObservableObject`) giữ state UI (filter, sort, trang hiện tại, danh sách Items) và expose `ICommand` để gọi Use Case.
- **Quan hệ với layer khác**: ViewModel gọi trực tiếp các Use Case trong Domain layer và lắng nghe sự kiện `DataChanged` từ `SyncService` (Infrastructure) để reload dữ liệu; View chỉ bind vào ViewModel qua data binding/commands.

### Domain layer (Domain Module)

- **Vai trò chính**: Chứa toàn bộ nghiệp vụ lõi (business rules) và mô hình domain độc lập với chi tiết kỹ thuật lưu trữ hay UI.
- **Thành phần tiêu biểu**:
  - **Entities**: `DeviceModel`, `BorrowedDevice` mô tả thông tin thiết bị trong kho và thiết bị đã mượn.
  - **Value Objects**: `PagedResult<T>` (kết quả phân trang), `QueryParameters` (tham số filter/sort/pagination).
  - **Interfaces (repository contracts)**: `IDeviceModelRepository`, `IBorrowedDeviceRepository` định nghĩa các thao tác truy vấn/mượn/trả mà không gắn với SQLite.
  - **Use Cases**: `GetDeviceModelsUseCase`, `GetCategoriesUseCase`, `BorrowDeviceUseCase`, `GetBorrowedDevicesUseCase`, `ReturnDeviceUseCase` thực thi từng luồng nghiệp vụ cụ thể, chỉ phụ thuộc vào interfaces và entities/value objects.
- **Quan hệ với layer khác**: Domain không phụ thuộc Presentation/Data/Infrastructure; Data layer implement các repository interface, Presentation layer chỉ gọi Use Case đã được DI từ Domain.

### Data layer (Data Module)

- **Vai trò chính**: Cung cấp hiện thực cụ thể cho các repository trong Domain, làm việc với SQLite và tối ưu truy vấn.
- **Thành phần tiêu biểu**:
  - **Interfaces (data source contracts)**: `ISqliteDataSource` trừu tượng hóa việc lấy connection, khởi tạo schema, thực thi lệnh SQL.
  - **Repositories**: `DeviceModelRepository`, `BorrowedDeviceRepository` implement lần lượt `IDeviceModelRepository`, `IBorrowedDeviceRepository`, chịu trách nhiệm build câu lệnh SQL (WHERE, ORDER BY, LIMIT/OFFSET) dựa trên `QueryParameters`.
  - **Data Sources**: `SqliteDataSource` implement `ISqliteDataSource`, tạo database `devices.db`, thiết lập WAL, index và seed 1.000.000 bản ghi mẫu.
- **Quan hệ với layer khác**: Use Case (Domain) chỉ nhìn thấy interface repository; Repositories trong Data layer dùng `ISqliteDataSource` để làm việc với SQLite mà không lộ chi tiết này ra ngoài Domain/Presentation.

### Infrastructure layer (Infrastructure Module)

- **Vai trò chính**: Chứa các thành phần hạ tầng dùng chung, không thuộc nghiệp vụ lõi nhưng cần cho vận hành hệ thống (đa instance, đồng bộ, quản lý InstanceId).
- **Thành phần tiêu biểu**:
  - **`SyncService`**: gửi và lắng nghe UDP broadcast `DATA_CHANGED|{instanceId}` trên port 54321; raise event `DataChanged` để ViewModel reload dữ liệu khi có thay đổi từ instance khác.
  - **`InstanceSlotManager`**: quản lý file `instance_slots.json` trong `%LocalAppData%`, gán và tái sử dụng `InstanceId` cho từng process/slot, bảo đảm mỗi instance có danh sách \"My Device\" riêng.
- **Quan hệ với layer khác**: `SyncService` được ViewModels (Presentation) subscribe để cập nhật UI; `InstanceSlotManager` được dùng khi khởi động app (trong `App.xaml.cs`) để lấy `InstanceId` cung cấp cho Domain/Data khi mượn/trả và truy vấn theo instance.

---

## 6. Cấu trúc dự án

```
device_management/
├── App1/
│   ├── App.xaml / App.xaml.cs          # Khởi động, DI, InstanceId, DB init, Sync
│   ├── MainWindow.xaml / .cs           # Shell: NavigationView, Frame, 2 menu
│   ├── App1.csproj
│   │
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── DeviceModel.cs
│   │   │   └── BorrowedDevice.cs
│   │   ├── ValueObjects/
│   │   │   ├── PagedResult.cs
│   │   │   └── QueryParameters.cs
│   │   ├── Interfaces/
│   │   │   ├── IDeviceModelRepository.cs
│   │   │   └── IBorrowedDeviceRepository.cs
│   │   └── UseCases/
│   │       ├── GetDeviceModelsUseCase.cs
│   │       ├── GetCategoriesUseCase.cs
│   │       ├── BorrowDeviceUseCase.cs
│   │       ├── GetBorrowedDevicesUseCase.cs
│   │       └── ReturnDeviceUseCase.cs
│   │
│   ├── Data/
│   │   ├── Interfaces/
│   │   │   └── ISqliteDataSource.cs
│   │   ├── DataSources/
│   │   │   └── SqliteDataSource.cs      # Schema, index, seed 1M records
│   │   └── Repositories/
│   │       ├── DeviceModelRepository.cs
│   │       └── BorrowedDeviceRepository.cs
│   │
│   ├── Infrastructure/
│   │   ├── SyncService.cs              # UDP broadcast/listen port 54321
│   │   └── InstanceSlotManager.cs      # Slot + persist InstanceId
│   │
│   └── Presentation/
│       ├── ViewModels/
│       │   ├── RequestDeviceViewModel.cs
│       │   └── MyDeviceViewModel.cs    # + SelectableDevice, PageItem
│       ├── Views/
│       │   ├── RequestDevicePage.xaml / .cs
│       │   └── MyDevicePage.xaml / .cs
│       └── Converters/
│           └── AvailableToBoolConverter.cs
│
├── Requrements.md                       # UC_01–UC_06 chi tiết
├── SYSTEM.md                            # Tài liệu này
└── device_management_system_*.plan.md   # Kế hoạch triển khai (nếu có)
```

---

## 7. Công nghệ và thư viện

| Thành phần | Công nghệ / Package |
|------------|----------------------|
| UI | WinUI 3, Windows App SDK |
| Framework | .NET 8, C# 12 |
| MVVM | CommunityToolkit.Mvvm |
| DataGrid | CommunityToolkit.WinUI.UI.Controls.DataGrid |
| Database | Microsoft.Data.Sqlite |
| DI | Microsoft.Extensions.DependencyInjection |

**Target**: `net8.0-windows10.0.19041.0`, nền tảng: x86, x64, ARM64.

---

## 8. Mô hình dữ liệu

### 6.1 Entity Domain

- **DeviceModel**: `Id`, `Model`, `Manufacturer`, `Category`, `SubCategory`, `Available`, `Reserved`.
- **BorrowedDevice**: `Id`, `DeviceModelId`, `ModelName`, `IMEI`, `Label`, `SerialNumber`, `CircuitSerialNumber`, `HWVersion`, `BorrowedDate`, `ReturnDate`, `Invoice`, `Status`, `Inventory`, `InstanceId`.

### 6.2 SQLite Schema

- **DeviceModels**: Bảng model kho; index trên `Model`, `Manufacturer`, `Category`, `SubCategory`.
- **BorrowedDevices**: Bảng thiết bị đã mượn; FK `DeviceModelId`, index `InstanceId`, `DeviceModelId`.
- **Journal**: WAL; **Synchronous**: NORMAL (cân bằng an toàn và hiệu năng).
- **Seed**: 1.000.000 bản ghi mẫu (batch 50.000), `Available` 1–20, `Reserved` = 0 lúc khởi tạo.

### 6.3 Value Objects

- **PagedResult&lt;T&gt;**: `Items`, `TotalCount`, `Page`, `PageSize`, `TotalPages`.
- **QueryParameters**: `Filters` (Dictionary), `SortColumn`, `SortAscending`, `Page`, `PageSize`.

---

## 9. Luồng nghiệp vụ chính

### 7.1 Request Device (Xem / Lọc / Sort / Mượn)

1. User mở trang **Request Device** → `RequestDeviceViewModel.LoadDataAsync()` gọi `GetDeviceModelsUseCase` với `QueryParameters` (filter, sort, page, pageSize).
2. `DeviceModelRepository.GetPagedAsync()` build SQL động (WHERE, ORDER BY), thực thi và trả `PagedResult<DeviceModel>`.
3. Filter: text (Model, Manufacturer) debounce 300ms; Category/SubCategory qua ComboBox (data từ `GetCategoriesUseCase` / `GetDistinctSubCategoriesAsync`).
4. User bấm **Borrow** → ContentDialog nhập số lượng → `BorrowDeviceUseCase.ExecuteAsync(modelId, quantity, App.InstanceId)` → Repository giảm `Available`, tăng `Reserved`, insert vào `BorrowedDevices` với `InstanceId`; sau đó `SyncService.Broadcast()`.
5. Phân trang: tối đa 5 nút trang (sliding window), kích thước trang 10/25/50/100.

### 7.2 My Device (Xem / Lọc / Sort / Trả)

1. User mở trang **My Device** → `MyDeviceViewModel.LoadDataAsync()` gọi `GetBorrowedDevicesUseCase` với `QueryParameters` và **App.InstanceId**.
2. `BorrowedDeviceRepository.GetPagedAsync(query, instanceId)` chỉ lấy bản ghi `WHERE InstanceId = @inst`.
3. User chọn một hoặc nhiều dòng (checkbox), bấm **Return Selected** → xác nhận → `ReturnDeviceUseCase.ExecuteAsync(selectedIds)` → xóa/trả thiết bị, cập nhật lại `DeviceModels` (Available/Reserved), `SyncService.Broadcast()`.

### 7.3 InstanceId và danh sách “My Device”

- **InstanceId** do `InstanceSlotManager.GetOrCreateInstanceId()` cấp khi khởi động app: mỗi process nhận một **slot** (lưu trong file), slot gắn với một InstanceId cố định.
- Mở lại app → process mới nhận slot đã free (PID cũ không còn) → dùng lại InstanceId đã lưu → danh sách My Device vẫn đúng.
- Nhiều instance chạy cùng lúc → mỗi instance một slot/InstanceId khác nhau → mỗi cửa sổ chỉ thấy thiết bị đã mượn của chính instance đó.

---

## 10. Đa instance và đồng bộ

- **SyncService**: UDP broadcast trên port **54321**, message dạng `DATA_CHANGED|{instanceId}`. Instance nhận message (và không phải chính nó) thì raise `DataChanged` → ViewModel reload dữ liệu (Request Device / My Device).
- **InstanceSlotManager**: File `%LocalAppData%\DeviceManagement\instance_slots.json` lưu danh sách slot (InstanceId + Pid). Process mới tìm slot trống (Pid = 0 hoặc process không còn chạy), gán Pid hiện tại và dùng hoặc tạo InstanceId cho slot đó.
- **Kết quả**: Đồng bộ danh sách model giữa các instance; danh sách My Device tách biệt theo từng instance, không lẫn thiết bị của instance khác.

---

## 11. Cấu hình và lưu trữ

| Nội dung | Đường dẫn / Giá trị |
|----------|----------------------|
| SQLite DB | `%LocalAppData%\App1\devices.db` |
| Instance slots | `%LocalAppData%\DeviceManagement\instance_slots.json` |
| UDP sync | Port **54321**, broadcast `DATA_CHANGED|{instanceId}` |
| Kích thước trang mặc định | 50 (10/25/50/100 tùy chọn) |
| Số nút trang tối đa | 5 (sliding window) |

---

## 12. Build và chạy ứng dụng

### Yêu cầu

- Windows 10/11 (19041+).
- .NET 8 SDK.
- Visual Studio 2022 hoặc dotnet CLI với workload WinUI/Windows.

### Lệnh

```bash
cd device_management/App1
dotnet restore
dotnet build
dotnet run
```

Hoặc mở solution/project trong Visual Studio và chạy (F5).

### Ghi chú

- Lần chạy đầu tiên có thể mất vài giây để tạo DB và seed 1.000.000 bản ghi.
- Để reset dữ liệu: xóa `%LocalAppData%\App1\devices.db` (và nếu cần, `instance_slots.json` để reset slot/InstanceId).

---

## 13. Sequence Diagrams (PlantUML)

Sequence diagram cho từng use case được mô tả bằng **PlantUML** trong thư mục `docs/`:

| File | Nội dung |
|------|----------|
| `docs/sequence-diagrams.puml` | 6 diagram: UC01 (Xem danh sách Model), UC02 (Lọc Model), UC03 (Sort Model), UC04 (Mượn Model), UC05 (Trả Model), UC06 (Đồng bộ giữa các Instance) |
| `docs/README-sequence-diagrams.md` | Hướng dẫn xem và xuất hình (PlantUML server, VS Code, CLI) |
| `docs/architecture-views.puml` | Module View + Component & Connector View (PlantUML) |
| `assets/diagrams/06-module-view-client-only-deployment.mmd` | Module View – Client-Only Deployment (Mermaid): các module trong app WinUI 3, local storage, UDP sync giữa các instance |
| `assets/diagrams/07-component-connector-view.mmd` | Component & Connector View (C&C): các component runtime và connector (method call, data binding, SQL, UDP, file I/O) |

Mỗi diagram mô tả luồng: **User → View → ViewModel → UseCase → Repository → SqliteDataSource / SQLite**; UC04 và UC05 có thêm **SyncService.Broadcast()**; UC06 mô tả **Instance A broadcast → Instance B nhận DataChanged → reload dữ liệu**.

Để vẽ/xuất hình: dùng [PlantUML Server](http://www.plantuml.com/plantuml/uml/), extension PlantUML trong VS Code, hoặc lệnh `java -jar plantuml.jar` (xem chi tiết trong `docs/README-sequence-diagrams.md`).

---

*Tài liệu hệ thống — Device Management, WinUI 3.*
