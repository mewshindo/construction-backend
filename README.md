## Установка и Запуск

Для запуска всех сервисов выполните команду:

```bash
cd construction-backend
docker-compose up -d
```

Это запустит следующие сервисы:
- API Gateway (порт 8000)
- Сервис заказов (порт 8001)
- Сервис пользователей (порт 8002)


После запуска, документация API (Swagger) по следующим адресам:

- API Gateway: http://localhost:8000/swagger
- Сервис заказов: http://localhost:8001/swagger
- Сервис юзеров: http://localhost:8002/swagger

## Структура проекта

```
construction-backend/
├── construction-apigateway/     # API Gateway
├── construction-service-orders/ # Сервис заказов
├── construction-service-users/  # Сервис пользователей
└── docs/                       # Документация API
```

### Основные endpoints

#### Сервис пользователей
- POST `/auth/register` - Регистрация нового пользователя
- POST `/auth/login` - Аутентификация пользователя
- GET `/users` - Получение списка пользователей
- GET `/users/{uuid}` - Получение информации о конкретном пользователе

#### Сервис заказов
- POST `/orders` - Создание нового заказа
- GET `/orders` - Получение списка заказов
- GET `/orders/{id}` - Получение информации о конкретном заказе
- PUT `/orders/{id}` - Обновление заказа
- POST `/orders/{id}/cancel` - Отмена заказа

### Тестирование

Для запуска тестов:

```bash
docker-compose run service_users dotnet test
docker-compose run service_orders dotnet test
```

## Безопасность

- Все эндпоинты, кроме `/auth/register` и `/auth/login`, требуют JWT токен
- Токены имеют срок действия 1 час
- Пароли хешируются
