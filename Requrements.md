Requrements

Functional Requirements  
	  
UC\_01. Xem danh sách Model trong kho  
UC\_01	Xem danh sách Model trong kho  
Description	Cho phép User xem danh sách các model thiết bị hiện có trong kho  
Actor	User, SQLite Database  
Pre-condition	•	Ứng dụng đang chạy  
•	Kết nối database thành công  
Post-condition	Danh sách model được hiển thị theo trang hiện tại  
Basic Flow	1\.	User mở trang “Request Device”  
2\.	System truy vấn danh sách model từ database  
3\.	System áp dụng phân trang  
4\.	Database trả về dữ liệu  
5\.	System hiển thị danh sách cho User  
Additional Flow	N/A

UC\_02. Lọc Model  
UC\_02	Lọc Model  
Description	Cho phép User lọc danh sách model theo các tiêu chí (Model, Manufacturer, Category, SubCategory  
Actor	User, SQLite Database  
Pre-condition	•	Danh sách model đang được hiển thị  
Post-condition	Danh sách chỉ chứa các model thỏa mãn điều kiện lọc  
Basic Flow	1\.	User nhập/chọn điều kiện lọc  
2\.	System gửi truy vấn có điều kiện đến database  
3\.	Database trả dữ liệu đã lọc  
4\.	System cập nhật danh sách hiển thị  
Additional Flow	N/A

UC\_03. Sort Model  
UC\_03	Lọc Model  
Description	Cho phép User sắp xếp danh sách model theo tiêu chí mong muốn  
Actor	User, SQLite Database  
Pre-condition	•	Danh sách model đang được hiển thị  
Post-condition	Danh sách được sắp xếp theo thứ tự mới  
Basic Flow	1\.	User chọn cột cần sắp xếp(ASC/DESC)  
2\.	System gửi truy vấn sắp xếp đến database  
3\.	Database trả dữ liệu đã sắp xếp  
4\.	System cập nhật danh sách hiển thị  
Additional Flow	N/A

UC\_04. Mượn Model  
UC\_04	Mượn Model  
Description	Cho phép User mượn model có sẵn trong kho  
Actor	User  
Pre-condition	•	User đang xem danh sách model trong kho  
•	Model còn thiết bị khả dụng  
Post-condition	•	Thiết bị chuyển sang trạng thái “Occupied”  
•	Số lượng khả dụng giảm tương ứng  
•	Các Instance khác được đồng bộ dữ liệu  
Basic Flow	1\.	User chọn một model trong danh sách và nhấn nút “Borrow”  
2\.	System hiển thị ContentDialog “Borrow Device” bao gồm:  
•	Thông tin model   
•	ComboBox số lượng muốn mượn  
3\.	User chọn số lượng và nhấn “Ok”  
4\.	Database kiểm tra số lượng  
5\. 	Database trả về kết quả  
6\.	System cập nhật lại danh sách hiển thị  
7\.	System thông báo kết quả thành công cho User  
Additional Flow  \[Nếu không đủ số lượng\] Thông báo lỗi tới User

UC\_05. Trả Model  
UC\_05	Trả Model  
Description	Cho phép User trả lại một hoặc nhiều thiết bị đã mượn  
Actor	User System SQLite Database  
Pre-condition	•	User đang xem danh sách model đã mượn  
•	User có ít nhất một thiết bị đã mượn  
Post-condition	•	Danh sách thiết bị đã mượn được cập nhật  
•	Các Instance khác được đồng bộ dữ liệu  
Basic Flow	1\.	User chọn một hoặc nhiều thiết bị (checkbox) và nhấn nút “Return Selected”  
2\.	System hiển thị ContentDialog “Confirm Return” bao gồm:  
•	Danh sách thiết bị được chọn  
•	Yêu cầu xác nhận thao tác  
3\.	User nhấn “Confirm”  
4\.         Database cập nhật số lượng của model  
5\.	System cập nhật lại danh sách hiển thị  
6\.	System thông báo kết quả thành công  
Additional Flow	N/A

UC\_06. Đồng bộ trạng thái giữa các Instance  
UC\_05	Đồng bộ trạng thái giữa các Instance  
Description	Đảm bảo khi một phiên bản ứng dụng thay đổi trạng thái (mượn hoặc trả), các phiên bản khác đang chạy sẽ cập nhật dữ liệu để hiển thị thông tin nhất quán  
Actor	•	System  
•	Other Application Instance  
Pre-condition	•	Có ít nhất 2 phiên bản ứng dụng đang chạy đồng thời   
•	Có thay đổi dữ liệu trong ứng dụng  
Post-condition	Tất cả các phiên bản ứng dụng đang hoạt động hiển thị dữ liệu cập nhật và nhất quán  
Basic Flow	1\.	Một phiên bản ứng dụng thực hiện thay đổi trạng thái (mượn hoặc trả) và cập nhật lên Database  
2\.	System ghi nhận thay đổi  
3\.	System phát tín hiệu thông báo có dữ liệu thay đổi  
4\.	Các phiên bản ứng dụng khác nhận được thông báo và tải lại dữ liệu mới nhất  
5\.	Các phiên bản đó làm mới dữ liệu hiển thị  
Additional Flow	N/A

