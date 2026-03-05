---
name: Fix pagination button flickering
overview: Remove the IsLoading-based button disable/enable from the pagination bar since in-memory operations are near-instant and CancellationTokenSource already handles concurrency. Buttons will only update state when data actually arrives (via Items property change).
todos:
  - id: fix-request-page
    content: Xoa IsLoading case trong OnViewModelPropertyChanged va bo !_vm.IsLoading trong UpdatePaginationUI cua RequestDevicePage.xaml.cs
    status: completed
  - id: fix-my-page
    content: Tuong tu cho MyDevicePage.xaml.cs
    status: completed
  - id: verify-build
    content: Kiem tra linter errors va build thanh cong
    status: completed
isProject: false
---

# Fix nhap nhay nut Pagination khi spam click

## Nguyen nhan

Moi click "Next" tao 3 lan thay doi trang thai button trong < 50ms:

1. `IsLoading = true` -> disable 4 buttons (qua `OnViewModelPropertyChanged`)
2. `IsLoading = false` -> enable 4 buttons (qua `OnViewModelPropertyChanged`)
3. `Items` thay doi -> `UpdatePaginationUI()` set lai 4 buttons

Voi in-memory cache, buoc 1-2 xay ra trong ~20-50ms -> user thay nhap nhay.

## Giai phap

Bo toan bo logic disable/enable buttons dua tren `IsLoading`. Chi cap nhat trang thai button khi du lieu thuc su den (qua `UpdatePaginationUI()` khi `Items` thay doi). `CancellationTokenSource` da bao ve chong du lieu cu roi.

## Thay doi cu the

### 1. [RequestDevicePage.xaml.cs](App1/Presentation/Views/RequestDevicePage.xaml.cs)

**a) Xoa case `IsLoading` trong `OnViewModelPropertyChanged` (dong 42-46):**

```csharp
// TRUOC
case nameof(RequestDeviceViewModel.IsLoading):
    FirstBtn.IsEnabled = !_vm.IsLoading && _vm.CanGoFirst;
    PrevBtn.IsEnabled = !_vm.IsLoading && _vm.CanGoPrevious;
    NextBtn.IsEnabled = !_vm.IsLoading && _vm.CanGoNext;
    LastBtn.IsEnabled = !_vm.IsLoading && _vm.CanGoLast;
    break;

// SAU: xoa toan bo case nay
```

**b) Trong `UpdatePaginationUI()` (dong 591-594), bo `!_vm.IsLoading &&`:**

```csharp
// TRUOC
FirstBtn.IsEnabled = !_vm.IsLoading && _vm.CanGoFirst;

// SAU
FirstBtn.IsEnabled = _vm.CanGoFirst;
```

### 2. [MyDevicePage.xaml.cs](App1/Presentation/Views/MyDevicePage.xaml.cs)

Ap dung tuong tu:

**a) Xoa case `IsLoading` trong `OnViewModelPropertyChanged` (dong 39-43)**

**b) Trong `UpdatePaginationUI()` (dong 382-385), bo `!_vm.IsLoading &&`**

## Ket qua

- Buttons chi cap nhat trang thai 1 lan duy nhat khi `Items` thay doi -> khong con nhap nhay
- `IsLoading` property van ton tai trong ViewModel de dung cho muc dich khac (vi du: hien thi loading indicator trong tuong lai)

