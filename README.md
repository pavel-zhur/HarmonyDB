# HarmonyDB
Musical harmony analysis database, song chord library, jam club library, and more.

This repository contains source code for a personal project with mixed purposes that has changed and evolved over time, built around my music hobby. The project is live and hosted on Azure. A somewhat limited public preview is available at https://с.harmonydb.com/.

## Planned and under development goals
- A harmony index built on thousands of songs, containing statistics of chord progression movements and options, supplemented by music-theoretical analysis features (modulations, cadences, chord sequence connections and mutations, etc.), associated with the songs' metadata (years, genres, countries), built as an open-source database engine responding to any kind of complex query, with a public API.
  - Development is ongoing. The plan is to support more and more query types, of any complexity, as soon as there is a new use case. At this early stage, the set of potential use cases is unpredictable, and the potential seems large.
- A front-end that allows users to query the index, get results with visualizations in the songs, save and publish the searches and results with the wiki functionality, allowing the community to share and discuss their music analysis right there.
  - Simplistic and barebone.
- A visual representation of harmony movements, trends and possibilities, connected to the index data source. A helper navigator of possible harmony changes from one progression to another, certain modulations, and whatever else it may be.
  - It is unlikely that I will be able to accomplish this alone, as I am not experienced enough in both music theory and front-end development. I hope for a community collaboration one day.
- An open source backend for the distributed song chord library exchange network, with optional pluggable sources and conditional access or distribution rules, allowing any existing song chord collection to join the index network and/or use the index features to provide more functionality to their users (the index comes with the LGPL license).

## Projects and resources used
- The online tuner based on web audio api, https://github.com/qiuxiang/tuner
- Limitless - Responsive Web Application Kit, https://themeforest.net/item/limitless-responsive-web-application-kit/13080328
- Materials from https://muzland.info/, https://muzland.ru/

## Thanks for the inspiration
- Adam Neely, https://www.youtube.com/@AdamNeely
- Нескучный Саунд, https://www.youtube.com/@NeSkuSound
- Vitaly Pavlenko, https://github.com/vpavlenko
