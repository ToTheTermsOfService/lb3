# ЛР 3. Визначення аномалій. Варіант 6: Мережа

ML.NET 6.0, .NET 10, `RandomizedPcaTrainer`. Датасет: [Network Anomaly Detection Dataset](https://www.kaggle.com/datasets/malkasasbeh/network-anomaly-detection-dataset).

Повний звіт: [Reports/REPORT.md](Reports/REPORT.md).

## Запуск

```bash
dotnet run --project src/NetworkAnomaly.Trainer
dotnet run --project src/NetworkAnomaly.Predictor
```

- **Trainer** (частина 1): ділить опити на сесії, рахує прирости лічильників і розбиває дані за часом на train/validation/test. Далі підбирає передобробку, ранг і поріг на validation, оцінює модель на test (з базовими рішеннями та перевіркою на іншій сесії), зберігає `Models/network_anomaly_model.zip`, `Data/holdout_test.csv` і `Reports/roc_test.svg`.
- **Predictor** (частина 2): завантажує модель і прогнозує для CSV з опитами (за замовчуванням `Data/holdout_test.csv`). Колонки `class` і `session` необов'язкові. Можна передати свій файл: `dotnet run --project src/NetworkAnomaly.Predictor -- шлях.csv`. Шлях до моделі задається змінною `NAD_MODEL`.

## Результат (test)

AUC 0.865, виявлено атак 72.0%, хибних тривог 6.7% [95% ДІ 3.4–12.6%].
Об'ємні атаки виявляються на 97–100%, повільні HTTP-атаки (slowloris, slowpost) — на 15–33%.
