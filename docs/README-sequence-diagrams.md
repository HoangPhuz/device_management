# Sequence Diagrams — Device Management

Thư mục chứa **PlantUML** sequence diagram cho các use case của hệ thống. **Participants** dùng tên thành phần cụ thể (RequestDevicePage, ViewModel, UseCase, Repository, SQLite, SyncService...). **Messages** mô tả hành động sẽ thực hiện (Load, Lấy, Truy vấn, Gán, Render, Gọi Broadcast...).

## File

| File | Nội dung |
|------|----------|
| `sequence-diagrams.puml` | 6 diagram: UC01–UC06 |

## Các diagram

1. **UC01** — RequestDevicePage → ViewModel Load → UseCase Lấy danh sách → Repository Truy vấn → SQLite Thực thi SELECT → Gán Items → Render DataGrid
2. **UC02** — Cập nhật filter và load lại → Truy vấn có WHERE → Gán Items mới → Render cập nhật
3. **UC03** — Đặt SortColumn và load lại → Truy vấn có ORDER BY → Gán Items → Render theo thứ tự mới
4. **UC04** — Gọi Borrow → Repository UPDATE/INSERT → Gọi Broadcast() → Gửi UDP → LoadDataAsync refresh
5. **UC05** — Gọi ReturnSelectedAsync → Repository UPDATE/DELETE → Gọi Broadcast() → LoadDataAsync refresh
6. **UC06** — SyncService A Broadcast → UDP → SyncService B Raise DataChanged → ViewModel B LoadDataAsync → UseCase/Repository Truy vấn → Gán Items

## Cách xem / xuất hình

### 1. PlantUML server (online)

- Mở [PlantUML Server](http://www.plantuml.com/plantuml/uml/).
- Mở `sequence-diagrams.puml`, copy từng khối `@startuml(...)` đến `@enduml` vào ô nhập.
- Server sẽ render từng diagram.

### 2. VS Code

- Cài extension **PlantUML** (jebbs.plantuml).
- Mở `sequence-diagrams.puml`, `Alt+D` để preview, hoặc chuột phải → "Export Current Diagram".

### 3. Command line (Java + PlantUML jar)

```bash
# Cần Java và plantuml.jar
java -jar plantuml.jar docs/sequence-diagrams.puml -o out
# Hình sẽ nằm trong docs/out/ (mỗi @startuml một file PNG/SVG)
```

### 4. Mermaid (chỉ tham khảo)

Nếu dùng Mermaid thay vì PlantUML, có thể chuyển nội dung tương ứng từ file `.puml` sang cú pháp Mermaid sequence.

---

Participants: **Page, ViewModel, UseCase, Repository, SQLite, SyncService**. Messages mô tả hành động: Load, Lấy, Truy vấn, Thực thi, Gán, Render, Gọi Broadcast, Gửi UDP, Raise event.
