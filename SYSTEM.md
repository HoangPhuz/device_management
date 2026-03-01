# Tài liệu Hệ thống Quản lý Thiết bị (Device Management System)

Tài liệu mô tả toàn bộ kiến trúc, chức năng và triển khai của ứng dụng **Device Management** — ứng dụng desktop WinUI 3 quản lý danh sách model thiết bị trong kho, mượn/trả thiết bị và đồng bộ đa instance.

---

## Mục lục

1. [Tổng quan](#1-tổng-quan)
2. [Yêu cầu chức năng](#2-yêu-cầu-chức-năng)
3. [Kiến trúc hệ thống](#3-kiến-trúc-hệ-thống)
4. [Cấu trúc dự án](#4-cấu-trúc-dự-án)
5. [Công nghệ và thư viện](#5-công-nghệ-và-thư-viện)
6. [Mô hình dữ liệu](#6-mô-hình-dữ-liệu)
7. [Luồng nghiệp vụ chính](#7-luồng-nghiệp-vụ-chính)
8. [Đa instance và đồng bộ](#8-đa-instance-và-đồng-bộ)
9. [Cấu hình và lưu trữ](#9-cấu-hình-và-lưu-trữ)
10. [Build và chạy ứng dụng](#10-build-và-chạy-ứng-dụng)
11. [Sequence Diagrams (PlantUML)](#11-sequence-diagrams-plantuml)

---

## 1. Tổng quan

- **Mục đích**: Cho phép người dùng xem danh sách model thiết bị trong kho, lọc/sắp xếp, mượn thiết bị và xem/trả thiết bị đã mượn. Hệ thống hỗ trợ nhiều instance chạy đồng thời với đồng bộ dữ liệu real-time và phân tách danh sách "My Device" theo từng instance.
- **Nền tảng**: Windows (WinUI 3, Windows App SDK), .NET 8.
- **Giao diện**: Hai trang chính — **Request Device** (danh sách model kho) và **My Device** (thiết bị đã mượn), điều hướng bằng `NavigationView`, phân trang tối đa 5 nút trang.

---

## 2. Yêu cầu chức năng

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

## 3. Kiến trúc hệ thống

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
- **Infrastructure**: Đồng bộ đa instance (SyncService), quản lý slot/InstanceId (InstanceSlotManager).

---

## 4. Cấu trúc dự án

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

## 5. Công nghệ và thư viện

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

## 6. Mô hình dữ liệu

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

## 7. Luồng nghiệp vụ chính

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

## 8. Đa instance và đồng bộ

- **SyncService**: UDP broadcast trên port **54321**, message dạng `DATA_CHANGED|{instanceId}`. Instance nhận message (và không phải chính nó) thì raise `DataChanged` → ViewModel reload dữ liệu (Request Device / My Device).
- **InstanceSlotManager**: File `%LocalAppData%\DeviceManagement\instance_slots.json` lưu danh sách slot (InstanceId + Pid). Process mới tìm slot trống (Pid = 0 hoặc process không còn chạy), gán Pid hiện tại và dùng hoặc tạo InstanceId cho slot đó.
- **Kết quả**: Đồng bộ danh sách model giữa các instance; danh sách My Device tách biệt theo từng instance, không lẫn thiết bị của instance khác.

---

## 9. Cấu hình và lưu trữ

| Nội dung | Đường dẫn / Giá trị |
|----------|----------------------|
| SQLite DB | `%LocalAppData%\App1\devices.db` |
| Instance slots | `%LocalAppData%\DeviceManagement\instance_slots.json` |
| UDP sync | Port **54321**, broadcast `DATA_CHANGED|{instanceId}` |
| Kích thước trang mặc định | 50 (10/25/50/100 tùy chọn) |
| Số nút trang tối đa | 5 (sliding window) |

---

## 10. Build và chạy ứng dụng

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

## 11. Sequence Diagrams (PlantUML)

Sequence diagram cho từng use case được mô tả bằng **PlantUML** trong thư mục `docs/`:

| File | Nội dung |
|------|----------|
| `docs/sequence-diagrams.puml` | 6 diagram: UC01 (Xem danh sách Model), UC02 (Lọc Model), UC03 (Sort Model), UC04 (Mượn Model), UC05 (Trả Model), UC06 (Đồng bộ giữa các Instance) |
| `docs/README-sequence-diagrams.md` | Hướng dẫn xem và xuất hình (PlantUML server, VS Code, CLI) |

Mỗi diagram mô tả luồng: **User → View → ViewModel → UseCase → Repository → SqliteDataSource / SQLite**; UC04 và UC05 có thêm **SyncService.Broadcast()**; UC06 mô tả **Instance A broadcast → Instance B nhận DataChanged → reload dữ liệu**.

Để vẽ/xuất hình: dùng [PlantUML Server](http://www.plantuml.com/plantuml/uml/), extension PlantUML trong VS Code, hoặc lệnh `java -jar plantuml.jar` (xem chi tiết trong `docs/README-sequence-diagrams.md`).

---

*Tài liệu hệ thống — Device Management, WinUI 3.*
