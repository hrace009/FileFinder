# FileFinder

Aplikasi desktop **WPF (.NET 8)** untuk mencari file secara cepat di folder lokal, shared folder, maupun mapped drive berdasarkan daftar nama file.

---

## Fitur Utama

- **Pencarian multi-path** — tambahkan beberapa folder/drive sebagai sumber pencarian sekaligus
- **Tiga mode pencarian:**
  - `Contains` — nama file mengandung kata kunci
  - `Exact` — nama file sama persis
  - `Wildcard` — pola wildcard (`*`, `?`)
- **Filter ekstensi** — batasi pencarian pada tipe file tertentu (misal: `.pdf`, `.docx`, `.xlsx`)
- **Rekursif** — telusuri subfolder secara otomatis (dapat dinonaktifkan)
- **Case-sensitive** — pencarian peka huruf besar/kecil (opsional)
- **Indeksasi dua fase** — membangun indeks file sekali, lalu cocokkan semua nama input secara efisien (~50× lebih cepat dari pendekatan naïf)
- **Pencarian async** — UI tetap responsif selama proses berjalan, dengan progress bar dan spinner
- **Batalkan pencarian** — tekan `Esc` atau tombol Batal kapan saja
- **Status hasil:** `Found`, `Not Found`, `Multiple Found` (dengan warna berbeda)
- **Ekspor hasil** — simpan hasil pencarian ke file

## Shortcut Keyboard

| Tombol | Aksi |
|--------|------|
| `F5`   | Mulai pencarian |
| `Esc`  | Batalkan pencarian |

## Prasyarat

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows)
- Windows 10/11 (aplikasi WPF)

## Cara Build & Jalankan

```bash
# Clone repository
git clone https://github.com/<username>/FileFinder.git
cd FileFinder

# Build & jalankan
dotnet run --project FileFinder/FileFinder.csproj
```

### Publish sebagai single-file executable (x64)

```bash
dotnet publish FileFinder/FileFinder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output tersedia di `FileFinder/bin/Release/net8.0-windows/win-x64/publish/`.

## Struktur Proyek

```
FileFinder/
├── Models/
│   ├── SearchConfig.cs      # Konfigurasi pencarian (path, mode, filter)
│   └── SearchResult.cs      # Model hasil pencarian beserta status
├── ViewModels/
│   ├── MainViewModel.cs     # ViewModel utama (MVVM)
│   └── RelayCommand.cs      # Implementasi ICommand
├── Services/
│   └── FileSearchService.cs # Logika pencarian & indeksasi file
├── Converters/
│   └── StatusColorConverter.cs  # Konverter warna untuk status hasil
├── MainWindow.xaml          # UI utama
└── App.xaml                 # Entry point aplikasi
```

## Teknologi

- **C# 12 / .NET 8**
- **WPF** (Windows Presentation Foundation)
- **MVVM Pattern**
- `IAsyncEnumerable` untuk streaming hasil pencarian secara real-time

---

## Lisensi & Hak Cipta

© 2026 Harris Yogasara ([mk.harris.y@pertamina.com](mailto:mk.harris.y@pertamina.com)). All rights reserved.
