# BirthdayPlugin

---

## Description
![image](https://user-images.githubusercontent.com/15903022/175820336-f3e4ca26-1df5-40ef-a16f-c3db71ded7ce.png)

---

## How to use this plugin

Add to bot's config file additional parameters:

### `Birthday`

Required parameter of `datetime` type, that tells bot your birthday and time when you prefer to receive message. Should be [ISO 8601](https://en.wikipedia.org/wiki/ISO_8601) format.

Example:

```
"Birthday": "2022-01-14T17:05:00Z" 
```

### `BirthdayName`

Optional parameter of `string` type, if it's specified - bot will use it as your name when wishing you happy birthday.

Example:

```
"BirthdayName": "Floofie"
```

![downloads](https://img.shields.io/github/downloads/Rudokhvist/BirthdayPlugin/total.svg?style=social)
