<img width="337" height="339" alt="image" src="https://github.com/user-attachments/assets/d7e7e6e0-e6f2-4607-a02a-97cc914e034f" />

# GameDash — Motor de Web Scraping Multicore + Página Web de Ofertas de Videojuegos

GameDash es un proyecto académico desarrollado como parte del III Proyecto de Programación Multicore.  
El sistema combina **web scraping en paralelo**, **procesamiento multicore**, y una **interfaz web moderna** para mostrar información consolidada sobre videojuegos, como:

- Precios en distintas tiendas digitales
- Descuentos
- Horas de juego estimadas
- Calificaciones (usuarios / críticas)
- Imágenes de portada
- Tipos de ediciones
- Enlaces directos de compra

Todo esto se recopila mediante **web scraping avanzado** desde múltiples fuentes y se sirve a una página web que muestra los datos de manera limpia y amigable.

---

# Características Principales

### Backend de Web Scraping Multicore
- Scraping en paralelo utilizando **Task Parallelism** en C#
- Integración con:
  - **Steam Store**
  - **Eneba**
  - **Fanatical (via Puppeteer Sharp)**
  - *(Opcional: soporte experimental para CDKeys, G2A…)*
- Generación de archivo `resultados.json` con toda la data consolidada
- Extracción de:
  - Precios
  - Descuentos
  - Calificaciones
  - Horas de juego (SteamSpy + HLTB)
  - Imágenes (Steam CDN)
- Manejo de errores, timeouts y fallback
- Desacople modular para agregar nuevas tiendas fácilmente

### Frontend — GameDash (Página Web)
- Interfaz moderna desarrollada por el equipo frontend
- Cargado dinámico de `resultados.json`
- Tarjetas visuales por cada videojuego
- Filtros:
  - Por precio
  - Por tienda
  - Por calificación
- Buscador en tiempo real
- Diseño responsive
- Ideal para GitHub Pages

---

# 🏗 Arquitectura

