# Harmony DB
Welcome to this repository, a personal project with mixed purposes that has changed and evolved over time, along with my interests in music and .Net.

The project is up and running, hosted on Azure. It's still a work in progress, but I've made a part of it accessible for anyone curious. You can check out the current version here: https://c.harmonydb.com/.

## History
Originally started as a Telegram bot to keep track of songs for our jam sessions and a PDF generator for songbooks, this repository has grown into a diverse collection of tools, including:
- A mobile-friendly Blazor web assembly application that allows musicians to search and manage chords even offline, with features like fingerings view and a circle of fifts.
- Some musical harmony analysis features, like chord progression and loop detection, a degree view mode, and a (key-insensitive) song search based on progressions.
- A GPT-powered chatbot that loves to chat about music and create song images from lyrics.
- Support for private and shared playlists, plus a multi-tenancy setup for clubs seeking a digital home (if you're interested in setting up your space, just let me know!).

Moving forward, I plan to develop an open source harmonic database index engine with a public API, more on that in the next paragraph.

## Planned and under development goals
- A harmony index built on thousands of songs, containing statistics of chord progression movements and options, supplemented by music-theoretical analysis features (modulations, cadences, chord sequence connections and mutations, etc.), associated with the songs' metadata (years, genres, countries), built as an open-source database engine responding to any kind of complex query, with a public API.
  - Development is ongoing. The plan is to support more and more query types of any complexity as new use cases emerge. At this early stage, the set of potential use cases is unpredictable, and the potential seems large.
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
