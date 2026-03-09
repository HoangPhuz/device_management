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
- **Scope (Phạm vi phần mềm)**: Phần mềm bao gồm hai màn hình chính `Request Device` (xem danh sách model trong kho và mượn thiết bị) và `My Device` (xem và trả thiết bị đã mượn), hỗ trợ filter/sort/pagination trên tập dữ liệu lớn (500.000 model, mỗi model 2–3 thiết bị), sử dụng **in-memory caching** với LINQ để đạt thời gian phản hồi <1s, lưu trữ dữ liệu trên SQLite local và đồng bộ nhiều instance qua UDP broadcast. Ngoài phạm vi: không cung cấp server trung tâm, không có authentication/authorization, không xử lý báo cáo nâng cao hay tích hợp hệ thống bên ngoài.
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
9. [Cơ chế In-Memory Caching](#9-cơ-chế-in-memory-caching)
10. [Luồng nghiệp vụ chính](#10-luồng-nghiệp-vụ-chính)
11. [Đa instance và đồng bộ](#11-đa-instance-và-đồng-bộ)
12. [Cấu hình và lưu trữ](#12-cấu-hình-và-lưu-trữ)
13. [Build và chạy ứng dụng](#13-build-và-chạy-ứng-dụng)
14. [Sequence Diagrams (PlantUML)](#14-sequence-diagrams-plantuml)

---

## 2. Tổng quan

- **Mục đích**: Cho phép người dùng xem danh sách model thiết bị trong kho, lọc/sắp xếp, mượn thiết bị và xem/trả thiết bị đã mượn. Hệ thống hỗ trợ nhiều instance chạy đồng thời với đồng bộ dữ liệu real-time và phân tách danh sách "My Device" theo từng instance.
- **Nền tảng**: Windows (WinUI 3, Windows App SDK), .NET 8.
- **Giao diện**: Hai trang chính — **Request Device** (danh sách model kho) và **My Device** (thiết bị đã mượn), điều hướng bằng `NavigationView`, phân trang tối đa 5 nút trang với highlight trang hiện tại.
- **Hiệu năng**: Sử dụng **in-memory caching** để đạt thời gian phản hồi <1s cho filter/sort/pagination. Database chỉ được truy cập khi khởi động app hoặc khi thực hiện Borrow/Return.

---

## 3. Yêu cầu chức năng

| UC | Mô tả |
|----|--------|
| **UC_01** | Xem danh sách Model trong kho — phân trang, filter, sort từ in-memory cache. |
| **UC_02** | Lọc Model theo Name, Manufacturer, Category, SubCategory (kết hợp filter, debounce 300ms). |
| **UC_03** | Sắp xếp Model theo cột (ASC/DESC) sử dụng LINQ in-memory. |
| **UC_04** | Mượn Model — chọn số lượng qua ContentDialog, cập nhật DB và cache (optimistic update), đồng bộ instance khác. |
| **UC_05** | Trả thiết bị — chọn một hoặc nhiều Device (checkbox), xác nhận, cập nhật DB và cache (optimistic update), đồng bộ. |
| **UC_06** | Đồng bộ trạng thái giữa các instance — UDP broadcast khi có thay đổi; instance khác nhận và refresh cache từ DB. |
| **UC_07** | Xem danh sách Device đã mượn (My Device) — filter theo Name, IMEI, SerialLab, SerialNumber, CircuitSerialNumber, HWVersion, Inventory. |

**Ràng buộc hiệu năng**: Các thao tác filter/sort/pagination hoạt động trong thời gian < 1 giây nhờ in-memory caching với LINQ.

---

## 4. Yêu cầu phi chức năng (Non-functional Requirements)

- **Performance**: Các thao tác filter/sort/pagination hoàn thành trong < 1 giây với bộ dữ liệu 500.000 model, nhờ **in-memory caching** và LINQ. Database chỉ được truy cập lần đầu (lazy load) hoặc khi Borrow/Return. Debounce 300ms cho filter text giảm số lần query.
- **Reliability / Availability**: Ứng dụng chạy theo mô hình client-only, dữ liệu lưu trữ trên SQLite local; cơ chế `SyncService` sử dụng UDP broadcast giúp các instance đang hoạt động nhận biết thay đổi dữ liệu và refresh cache để giữ trạng thái hiển thị nhất quán.
- **Compatibility**: Hệ thống chạy trên Windows 10/11 (build 19041+), sử dụng .NET 8 (`net8.0-windows10.0.19041.0`), hỗ trợ các kiến trúc x86, x64, ARM64 và phụ thuộc vào WinUI 3, Windows App SDK.
- **Usability**: Giao diện người dùng chia thành hai trang chính (`Request Device`, `My Device`) với `NavigationView`, DataGrid hỗ trợ filter/sort trực quan, phân trang tối đa 5 nút (sliding window) với highlight màu cho trang hiện tại, và các `ContentDialog` rõ ràng cho thao tác mượn/trả và xác nhận.
- **Maintainability / Scalability**: Kiến trúc theo Clean Architecture kết hợp MVVM, tách biệt các layer Domain, Data, Presentation, Infrastructure và sử dụng dependency injection, giúp dễ mở rộng thêm use case, repository hoặc thay thế `ISqliteDataSource` mà không ảnh hưởng đến phần còn lại.
- **Security**: Hệ thống không triển khai authentication/authorization hay encryption; dữ liệu (SQLite DB, file cấu hình slot) nằm trong `%LocalAppData%` của từng user trên máy cục bộ, cần được bảo vệ bởi chính sách bảo mật của hệ điều hành và môi trường vận hành.

---

## 5. Kiến trúc hệ thống

Hệ thống áp dụng **Clean Architecture** với các lớp:

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation (WinUI 3)                   │
│  MainWindow, RequestDevicePage, MyDevicePage, ViewModels    │
│  Converters (BoolToBrush, PositiveIntToBrush)               │
└───────────────────────────────┬─────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────┐
│                      Domain                                  │
│  Entities, ValueObjects, Interfaces, Use Cases               │
└───────────────────────────────┬─────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────┐
│                        Data                                  │
│  Repositories (with In-Memory Cache), SqliteDataSource       │
└───────────────────────────────┬─────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────┐
│                   Infrastructure                             │
│  SyncService (UDP), InstanceSlotManager (persist InstanceId) │
└─────────────────────────────────────────────────────────────┘
```

- **Presentation**: MVVM (CommunityToolkit.Mvvm), ViewModels gọi Use Cases, không phụ thuộc trực tiếp vào Data/Infrastructure ngoài DI. Bao gồm Converters để transform data cho UI.
- **Domain**: Entities (`DeviceModel`, `Device`), Value Objects (`PagedResult<T>`, `QueryParameters`), Interfaces repository, Use Cases (single responsibility).
- **Data**: SQLite qua `ISqliteDataSource`, Repository triển khai **in-memory cache** với lazy loading và **LINQ filtering/sorting/pagination**.
- **Infrastructure**: Đồng bộ đa instance (`SyncService`), quản lý slot/`InstanceId` (`InstanceSlotManager`).

### Presentation layer (Presentation Module)

- **Vai trò chính**: Là lớp giao tiếp với người dùng, hiển thị dữ liệu và nhận thao tác (click, filter, sort, mượn/trả). Presentation không chứa nghiệp vụ lõi mà chỉ điều phối và binding dữ liệu.
- **Thành phần tiêu biểu**:
  - `MainWindow` và `NavigationView`: khung điều hướng giữa hai trang `RequestDevicePage` và `MyDevicePage`.
  - `Style.xaml`: Resource dictionary chứa các style dùng chung (nút, dialog, v.v.).
  - **Views**: `RequestDevicePage`, `MyDevicePage` hiển thị DataGrid, filter, pagination và các nút lệnh (Borrow, Return Selected).
  - **ViewModels**: `RequestDeviceViewModel`, `MyDeviceViewModel` (theo MVVM/`ObservableObject`) giữ state UI (filter, sort, trang hiện tại, danh sách Items) và expose `ICommand` để gọi Use Case. Bao gồm `PageItem` class cho pagination buttons và `SelectableDevice` wrapper cho checkbox selection.
  - **Converters**: `BoolToBrushConverter` (chuyển bool thành Brush cho pagination highlight), `PositiveIntToBrushConverter` (chuyển số thành màu dựa vào giá trị > 0), `AvailableToBoolConverter`.
- **Quan hệ với layer khác**: ViewModel gọi trực tiếp các Use Case trong Domain layer và lắng nghe sự kiện `DataChanged` từ `SyncService` (Infrastructure) để refresh cache và reload dữ liệu; View chỉ bind vào ViewModel qua data binding/commands.

### Domain layer (Domain Module)

- **Vai trò chính**: Chứa toàn bộ nghiệp vụ lõi (business rules) và mô hình domain độc lập với chi tiết kỹ thuật lưu trữ hay UI.
- **Thành phần tiêu biểu**:
  - **Entities**: `DeviceModel` mô tả thông tin model (kho); `Device` mô tả một thiết bị cụ thể trong kho (có `ModelId`, `Status`, `InstanceId`, v.v.).
  - **Value Objects**: `PagedResult<T>` (kết quả phân trang), `QueryParameters` (tham số filter/sort/pagination).
  - **Interfaces (repository contracts)**: `IDeviceModelRepository` định nghĩa thao tác paging, lấy danh sách Category/SubCategory, mượn, refresh cache và update cache; `IDeviceRepository` định nghĩa truy vấn thiết bị theo instance, trả thiết bị và refresh cache.
  - **Use Cases**: `GetDeviceModelsUseCase`, `GetCategoriesUseCase`, `BorrowDeviceUseCase`, `GetDevicesUseCase`, `ReturnDeviceUseCase` thực thi từng luồng nghiệp vụ cụ thể, chỉ phụ thuộc vào interfaces và entities/value objects.
- **Quan hệ với layer khác**: Domain không phụ thuộc Presentation/Data/Infrastructure; Data layer implement các repository interface, Presentation layer chỉ gọi Use Case đã được DI từ Domain.

### Data layer (Data Module)

- **Vai trò chính**: Cung cấp hiện thực cụ thể cho các repository trong Domain, làm việc với SQLite và **in-memory cache**.
- **Thành phần tiêu biểu**:
  - **Interfaces (data source contracts)**: `ISqliteDataSource` trừu tượng hóa việc lấy connection, khởi tạo schema, thực thi lệnh SQL.
  - **Repositories**:
    - `DeviceModelRepository` implement `IDeviceModelRepository`: **lazy-load cache** từ DB lần đầu, sau đó filter/sort/pagination bằng **LINQ in-memory**. Hỗ trợ `RefreshCacheAsync()` và `UpdateCachedModel()` cho optimistic updates.
    - `DeviceRepository` implement `IDeviceRepository`: **lazy-load cache** theo `instanceId`, filter/sort/pagination bằng **LINQ in-memory**. Hỗ trợ `RefreshCacheAsync()` và `RemoveFromCache()` sau khi return.
  - **Data Sources**: `SqliteDataSource` implement `ISqliteDataSource`, tạo database `devices.db` tại `%LocalAppData%\App1\`, thiết lập WAL, tạo index và seed dữ liệu từ file JSON (`SampleData/dbModels.json`, `SampleData/dbDevices.json`) khi bảng rỗng.
- **Quan hệ với layer khác**: Use Case (Domain) chỉ nhìn thấy interface repository; Repositories trong Data layer dùng `ISqliteDataSource` để làm việc với SQLite mà không lộ chi tiết này ra ngoài Domain/Presentation.

### Infrastructure layer (Infrastructure Module)

- **Vai trò chính**: Chứa các thành phần hạ tầng dùng chung, không thuộc nghiệp vụ lõi nhưng cần cho vận hành hệ thống (đa instance, đồng bộ, quản lý InstanceId).
- **Thành phần tiêu biểu**:
  - **`SyncService`**: gửi và lắng nghe UDP broadcast `DATA_CHANGED|{instanceId}` trên port 54321; raise event `DataChanged` để ViewModel refresh cache và reload dữ liệu khi có thay đổi từ instance khác.
  - **`InstanceSlotManager`**: quản lý file `instance_slots.json` trong `%LocalAppData%`, gán và tái sử dụng `InstanceId` cho từng process/slot, bảo đảm mỗi instance có danh sách "My Device" riêng.
- **Quan hệ với layer khác**: `SyncService` được ViewModels (Presentation) subscribe để cập nhật UI; `InstanceSlotManager` được dùng khi khởi động app (trong `App.xaml.cs`) để lấy `InstanceId` cung cấp cho Domain/Data khi mượn/trả và truy vấn theo instance.

---

## 6. Cấu trúc dự án

```
device_management/
├── App1/
│   ├── App.xaml / App.xaml.cs          # Khởi động, DI, InstanceId, DB init, Cache preload, Sync
│   ├── MainWindow.xaml / .cs           # Shell: NavigationView, Frame, 2 menu
│   ├── MainPage.xaml / .cs             # Trang mặc định (landing/placeholder)
│   ├── Style.xaml                      # Resource dictionary style dùng chung
│   ├── App1.csproj
│   │
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── DeviceModel.cs          # Id, Name, Manufacturer, Category, SubCategory, Available, Reserved
│   │   │   └── Device.cs               # Id, ModelId, Name, IMEI, SerialLab, SerialNumber, CircuitSerialNumber, HWVersion, Status, BorrowedDate, ReturnDate, Invoice, Inventory, InstanceId
│   │   ├── ValueObjects/
│   │   │   ├── PagedResult.cs
│   │   │   └── QueryParameters.cs
│   │   ├── Interfaces/
│   │   │   ├── IDeviceModelRepository.cs   # + RefreshCacheAsync(), UpdateCachedModel()
│   │   │   └── IDeviceRepository.cs        # + RefreshCacheAsync()
│   │   └── UseCases/
│   │       ├── GetDeviceModelsUseCase.cs   # + RefreshAsync()
│   │       ├── GetCategoriesUseCase.cs
│   │       ├── BorrowDeviceUseCase.cs
│   │       ├── GetDevicesUseCase.cs        # + RefreshAsync()
│   │       └── ReturnDeviceUseCase.cs
│   │
│   ├── Data/
│   │   ├── Interfaces/
│   │   │   └── ISqliteDataSource.cs
│   │   ├── DataSources/
│   │   │   └── SqliteDataSource.cs         # Schema, index, seed từ JSON file
│   │   └── Repositories/
│   │       ├── DeviceModelRepository.cs    # In-memory cache + LINQ filter/sort/pagination
│   │       └── DeviceRepository.cs         # In-memory cache (per instanceId) + LINQ
│   │
│   ├── Infrastructure/
│   │   ├── SyncService.cs                  # UDP broadcast/listen port 54321
│   │   └── InstanceSlotManager.cs          # Slot + persist InstanceId
│   │
│   └── Presentation/
│       ├── ViewModels/
│       │   ├── RequestDeviceViewModel.cs   # + PageItem class, OnCurrentPageChanged
│       │   └── MyDeviceViewModel.cs        # + SelectableDevice, filter fields (IMEI, SerialLab, etc.)
│       ├── Views/
│       │   ├── RequestDevicePage.xaml / .cs
│       │   └── MyDevicePage.xaml / .cs
│       └── Converters/
│           ├── AvailableToBoolConverter.cs
│           ├── BoolToBrushConverter.cs         # bool → Brush (pagination highlight)
│           └── PositiveIntToBrushConverter.cs  # int → Brush (Available/Reserved colors)
│
├── SampleData/
│   ├── dbModels.json                       # 500.000 bản ghi DeviceModel
│   ├── dbDevices.json                      # ~1.250.000 bản ghi Device (2–3 thiết bị/model)
│   └── SYSTEM.md                           # Tài liệu này
│
├── Requirements.md                         # UC_01–UC_07 chi tiết
└── device_management_system_*.plan.md      # Kế hoạch triển khai (nếu có)
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
| Caching | In-memory List<T> with SemaphoreSlim for thread-safety |
| Filtering/Sorting | LINQ (System.Linq) |

**Target**: `net8.0-windows10.0.19041.0`, nền tảng: x86, x64, ARM64.

---

## 8. Mô hình dữ liệu

### 8.1 Entity Domain

- **DeviceModel**: `Id`, `Name`, `Manufacturer`, `Category`, `SubCategory`, `Available`, `Reserved`.
- **Device**: `Id`, `ModelId`, `Name`, `IMEI`, `SerialLab`, `SerialNumber`, `CircuitSerialNumber`, `HWVersion`, `Status` (Available/Occupied), `BorrowedDate`, `ReturnDate`, `Invoice`, `Inventory`, `InstanceId`.

### 8.2 SQLite Schema

- **Models**: Bảng model kho; index trên `Name`, `Manufacturer`, `Category`, `SubCategory`.
- **Devices**: Bảng thiết bị; FK `ModelId → Models(Id)`; index trên `ModelId`, `InstanceId`, `Status`.
- **Journal**: WAL; **Synchronous**: NORMAL (cân bằng an toàn và hiệu năng).
- **Seed**: Đọc từ `SampleData/dbModels.json` và `SampleData/dbDevices.json` bằng streaming JSON (`JsonSerializer.DeserializeAsyncEnumerable`), batch commit mỗi 5.000 bản ghi.

### 8.3 Value Objects

- **PagedResult\<T\>**: `Items`, `TotalCount`, `Page`, `PageSize`, `TotalPages`.
- **QueryParameters**: `Filters` (Dictionary), `SortColumn`, `SortAscending`, `Page`, `PageSize`.

### 8.4 ViewModel Helper Classes

- **PageItem** (trong `RequestDeviceViewModel.cs`): `PageNumber`, `IsCurrent`, `IsEllipsis`, `DisplayText`, `IsClickable` — dùng cho pagination buttons với highlight.
- **SelectableDevice** (trong `MyDeviceViewModel.cs`): Wrapper của `Device` với `IsSelected` property cho checkbox binding.

---

## 9. Cơ chế In-Memory Caching

### 9.1 Kiến trúc Cache

Hệ thống sử dụng **in-memory caching** để đạt hiệu năng <1s cho các thao tác filter/sort/pagination:

```
┌─────────────────────────────────────────────────────────────┐
│                     ViewModel                                │
│  LoadDataAsync() → UseCase.ExecuteAsync(QueryParameters)     │
└───────────────────────────────┬─────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────┐
│                     Repository                               │
│  EnsureCacheAsync() → Load from DB (first time only)         │
│  LINQ Where/OrderBy/Skip/Take on List<T> cache               │
└───────────────────────────────┬─────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────┐
│                     SQLite (chỉ lần đầu)                     │
└─────────────────────────────────────────────────────────────┘
```

### 9.2 DeviceModelRepository Cache

- **Cache**: `List<DeviceModel>? _cache` — lưu toàn bộ 500.000 model
- **Thread-safety**: `SemaphoreSlim _cacheLock` để đảm bảo thread-safe lazy loading
- **Lazy Load**: `EnsureCacheAsync()` chỉ load từ DB lần đầu, các lần sau trả về cache
- **Filter/Sort/Pagination**: LINQ trên `_cache`:
  - `Where()` cho filter (Name, Manufacturer, Category, SubCategory)
  - `OrderBy()`/`OrderByDescending()` cho sort
  - `Skip().Take()` cho pagination
- **Optimistic Update**: Sau `BorrowAsync()`, gọi `UpdateCachedModel(modelId, -quantity, +quantity)` để cập nhật `Available`/`Reserved` trực tiếp trong cache mà không reload từ DB
- **Refresh**: `RefreshCacheAsync()` set `_cache = null` và reload — chỉ dùng khi nhận `SyncService.DataChanged` từ instance khác

### 9.3 DeviceRepository Cache

- **Cache**: `List<Device>? _cache` + `string? _cacheInstanceId` — lưu devices theo từng instanceId
- **Thread-safety**: `SemaphoreSlim _cacheLock`
- **Lazy Load**: `EnsureCacheAsync(instanceId)` load devices của instance hiện tại
- **Filter**: LINQ hỗ trợ filter theo Name, IMEI, SerialLab, SerialNumber, CircuitSerialNumber, HWVersion, Inventory, Status
- **Sort**: LINQ trên tất cả các cột có thể sort
- **Optimistic Update**: Sau `ReturnAsync()`, gọi `RemoveFromCache(deviceIds)` để xóa devices khỏi cache
- **Refresh**: `RefreshCacheAsync(instanceId)` set `_cache = null` và reload

### 9.4 Cache Invalidation Strategy

| Event | Action |
|-------|--------|
| **Borrow (same instance)** | `UpdateCachedModel()` — update Available/Reserved in-place |
| **Return (same instance)** | `RemoveFromCache()` + `UpdateCachedModel()` — remove devices and update model counts |
| **SyncService.DataChanged (other instance)** | `RefreshCacheAsync()` — full reload from DB |
| **App Startup** | `RefreshCacheAsync()` — preload cache |

---

## 10. Luồng nghiệp vụ chính

### 10.1 Request Device (Xem / Lọc / Sort / Mượn)

1. User mở trang **Request Device** → `RequestDeviceViewModel.LoadDataAsync()` gọi `GetDeviceModelsUseCase` với `QueryParameters` (filter, sort, page, pageSize).
2. `DeviceModelRepository.GetPagedAsync()`:
   - Gọi `EnsureCacheAsync()` để lấy cache (load từ DB nếu lần đầu)
   - Apply LINQ `Where()` cho filter, `OrderBy()` cho sort, `Skip().Take()` cho pagination
   - Trả `PagedResult<DeviceModel>`
3. Filter: text (Name, Manufacturer) debounce 300ms; Category/SubCategory qua ComboBox (data từ `GetCategoriesUseCase` / `GetDistinctSubCategoriesAsync`).
4. User bấm **Borrow** → ContentDialog nhập số lượng → `BorrowDeviceUseCase.ExecuteAsync(modelId, quantity, App.InstanceId)` → `DeviceModelRepository.BorrowAsync()`:
   - Kiểm tra `Available >= quantity` (từ DB để đảm bảo data integrity).
   - Tìm `quantity` Device có `Status = 'Available'` thuộc model.
   - Cập nhật Device: `Status = 'Occupied'`, gán `BorrowedDate`, `InstanceId`.
   - Cập nhật Model: `Available -= quantity`, `Reserved += quantity`.
   - **Optimistic cache update**: `UpdateCachedModel(modelId, -quantity, +quantity)`
   - `SyncService.Broadcast()`.
5. Phân trang: tối đa 5 nút trang (sliding window), kích thước trang 10/25/50/100, highlight trang hiện tại với màu `#5E8283`.

### 10.2 My Device (Xem / Lọc / Sort / Trả)

1. User mở trang **My Device** → `MyDeviceViewModel.LoadDataAsync()` gọi `GetDevicesUseCase` với `QueryParameters` và **App.InstanceId**.
2. `DeviceRepository.GetPagedAsync(query, instanceId)`:
   - Gọi `EnsureCacheAsync(instanceId)` để lấy cache
   - Apply LINQ filter cho Name, IMEI, SerialLab, SerialNumber, CircuitSerialNumber, HWVersion, Inventory
   - Trả `PagedResult<Device>`
3. User chọn một hoặc nhiều dòng (checkbox `SelectableDevice`), bấm **Return Selected** → xác nhận → `ReturnDeviceUseCase.ExecuteAsync(selectedIds)`:
   - Cập nhật `Models.Available += cnt`, `Reserved -= cnt` cho từng model tương ứng (DB).
   - Cập nhật `Devices.Status = 'Available'`, gán `ReturnDate`, xóa `InstanceId` (DB).
   - **Optimistic cache update**: `RemoveFromCache(deviceIds)` + `UpdateCachedModel()` cho từng model
   - `SyncService.Broadcast()`.

### 10.3 InstanceId và danh sách "My Device"

- **InstanceId** do `InstanceSlotManager.GetOrCreateInstanceId()` cấp khi khởi động app: mỗi process nhận một **slot** (lưu trong file), slot gắn với một InstanceId cố định.
- Mở lại app → process mới nhận slot đã free (PID cũ không còn) → dùng lại InstanceId đã lưu → danh sách My Device vẫn đúng.
- Nhiều instance chạy cùng lúc → mỗi instance một slot/InstanceId khác nhau → mỗi cửa sổ chỉ thấy thiết bị đã mượn của chính instance đó.

---

## 11. Đa instance và đồng bộ

- **SyncService**: UDP broadcast trên port **54321**, message dạng `DATA_CHANGED|{instanceId}`. Instance nhận message (và không phải chính nó) thì raise `DataChanged` → ViewModel gọi `RefreshAsync()` để reload cache từ DB, sau đó `LoadDataAsync()` để refresh UI.
- **InstanceSlotManager**: File `%LocalAppData%\DeviceManagement\instance_slots.json` lưu danh sách slot (InstanceId + Pid). Process mới tìm slot trống (Pid = 0 hoặc process không còn chạy), gán Pid hiện tại và dùng hoặc tạo InstanceId cho slot đó.
- **Kết quả**: Đồng bộ danh sách model giữa các instance; danh sách My Device tách biệt theo từng instance, không lẫn thiết bị của instance khác.

---

## 12. Cấu hình và lưu trữ

| Nội dung | Đường dẫn / Giá trị |
|----------|----------------------|
| SQLite DB | `%LocalAppData%\App1\devices.db` |
| Instance slots | `%LocalAppData%\DeviceManagement\instance_slots.json` |
| UDP sync | Port **54321**, broadcast `DATA_CHANGED|{instanceId}` |
| Dữ liệu seed | `SampleData/dbModels.json` (500.000 model), `SampleData/dbDevices.json` (~1.250.000 device) |
| Kích thước trang mặc định | 50 (10/25/50/100 tùy chọn) |
| Số nút trang tối đa | 5 (sliding window) |
| Debounce filter | 300ms |
| Pagination highlight | Background `#5E8283`, Foreground `#FFFFFF` |
| Normal page button | Background `#FFFFFF`, Foreground `#2C2C2C` |
| Available/Reserved > 0 | Color `#3276B1` |
| Available/Reserved = 0 | Color `#333333` |

---

## 13. Build và chạy ứng dụng

### Yêu cầu

- Windows 10/11 (19041+).
- .NET 8 SDK.
- Visual Studio 2022 hoặc dotnet CLI với workload WinUI/Windows.

### Lệnh

```bash
cd device_management/App1
dotnet restore
dotnet build -p:Platform=x64
dotnet run
```

Hoặc mở solution/project trong Visual Studio và chạy (F5).

### Ghi chú

- Lần chạy đầu tiên có thể mất **vài phút** để đọc và seed toàn bộ `dbModels.json` (500.000 bản ghi) và `dbDevices.json` (~1.250.000 bản ghi) vào SQLite.
- Sau khi seed xong, cache sẽ được preload và các thao tác filter/sort/pagination sẽ rất nhanh (<1s).
- Để reset dữ liệu: xóa `%LocalAppData%\App1\devices.db` (và nếu cần, `instance_slots.json` để reset slot/InstanceId).

---

## 14. Sequence Diagrams (PlantUML)

Sequence diagram cho từng use case được mô tả bằng **PlantUML** trong thư mục `docs/`:

| File | Nội dung |
|------|----------|
| `docs/sequence-diagrams.puml` | 6 diagram: UC01 (Xem danh sách Model), UC02 (Lọc Model), UC03 (Sort Model), UC04 (Mượn Device), UC05 (Trả Device), UC06 (Đồng bộ giữa các Instance) |
| `docs/README-sequence-diagrams.md` | Hướng dẫn xem và xuất hình (PlantUML server, VS Code, CLI) |
| `docs/architecture-views.puml` | Module View + Component & Connector View (PlantUML) |
| `assets/diagrams/06-module-view-client-only-deployment.mmd` | Module View – Client-Only Deployment (Mermaid): các module trong app WinUI 3, local storage, UDP sync giữa các instance |
| `assets/diagrams/07-component-connector-view.mmd` | Component & Connector View (C&C): các component runtime và connector (method call, data binding, SQL, UDP, file I/O) |

Mỗi diagram mô tả luồng: **User → View → ViewModel → UseCase → Repository (Cache) → SqliteDataSource / SQLite**; UC04 và UC05 có thêm **SyncService.Broadcast()** và **Optimistic Cache Update**; UC06 mô tả **Instance A broadcast → Instance B nhận DataChanged → RefreshCacheAsync() → reload dữ liệu**.

Để vẽ/xuất hình: dùng [PlantUML Server](http://www.plantuml.com/plantuml/uml/), extension PlantUML trong VS Code, hoặc lệnh `java -jar plantuml.jar` (xem chi tiết trong `docs/README-sequence-diagrams.md`).

---

*Tài liệu hệ thống — Device Management, WinUI 3.*
