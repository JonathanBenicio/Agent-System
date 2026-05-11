# GEMINI.md - Backend Context

Este arquivo define as regras e padrões específicos para o desenvolvimento do Backend neste projeto.

---

## 🚀 Stack Tecnológica
- **Language**: C# / .NET ou outra linguagem conforme o projeto.
- **Framework**: ASP.NET Core ou similar.
- **Persistence**: EF Core / Dapper / SQL direto.
- **Database**: PostgreSQL / SQL Server / SQLite.

---

## 🏗️ Arquitetura & Padrões

### Convenções de Codificação
- Siga as convenções idiomáticas da linguagem escolhida.
- Utilize injeção de dependência conforme os padrões do framework.
- Mantenha uma separação clara de responsabilidades (Clean Architecture, Hexagonal, etc.).

### Naming
- **Namespaces/Packages**: PascalCase ou conforme a linguagem.
- **Classes/Interfaces**: PascalCase.
- **Methods/Properties**: PascalCase ou camelCase dependendo da linguagem.

### Database
- Utilize migrations para gerenciar o esquema do banco de dados.
- Siga os padrões de nomenclatura (ex: snake_case para tabelas/colunas em PostgreSQL).

---

## ⚙️ Lógica & Performance
- Evite alocações desnecessárias e otimize loops críticos.
- Utilize fluxos assíncronos para operações de I/O.
- Implemente logs de auditoria e monitoramento em pontos críticos.

---

## ✅ Qualidade & Testes
- **Automated Tests**: Implemente testes unitários, de integração e funcionais.
- **Asserções**: Use as bibliotecas de teste padrão do ecossistema.
- **Patterns**: Siga o padrão Arrange-Act-Assert (AAA).

---

## 🤖 Instruções para o Agente
Ao trabalhar no diretório de backend:
1. Sempre verifique este arquivo e as configurações globais do projeto.
2. Siga os padrões arquiteturais já estabelecidos.
3. Garanta que o código seja testável e bem documentado.