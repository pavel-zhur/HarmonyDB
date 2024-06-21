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

## Front end features in detail
- Works as an app on Android, IPhone, desktop browsers (.Net 8 Blazor web assembly app).
- Responsive layout on mobile and desktop. On PC monitors, a library is displayed along with the chords (two-column layout). Quick jump to the top icons are provided. Loading indicators are shown, the app is responsive to user interaction.
- Allows to download the library offline (including shortlists, ratings, etc). Download works in the background, manually initiated, with progress displayed in the header.
- Search for songs - in the local library and in the index sources.
- Transposition, saving of favorite key, support of multiple chord versions of a song.
- Viewing favorite keys and favorite versions of other club members.
- Show all chords with flats or sharps.
- Correct lyrics wrapping on narrow screen, with chord positions maintained correctly.
- Actions work correctly in offline mode and sync when connected.
- Library actions work correctly when using multiple devices, conflicting changes are applied correctly.
- Recently viewed chords and recent searches are available.
- Songs are searchable by index (handy for jamming).
- A nice design theme is used.
- Telegram authentication.
- Books pdfs generation, ready for printing. The documents are created with the idea of minimizing the need to turn pages when playing a song.
- Multiple clubs support.
- Library supports custom user tags. Tags can be private or shared for viewing or editing.
- A circle of fifths shows the chords used in a song.
- Ability to select tonic and display degrees instead of notes for chord roots (e.g. 1m instead of Am).
- A nice chord parsing is implemented for playing and searching chords.
- Guitar tuner.
- Display of guitar chord fingerings. Song chords are clickable, the sound is played and a button appears to view the chords.
- Zooming the text, saving the convenient zoom factor on a device.
- Option to simplify chords: remove 7, 9, sus, basses, 11/13.
- Searching songs by chord progressions in the offline library, with the percentage of coverage displayed. Ability to enter the chord progression or select a progression in an existing song for the search.
- Detection of chord loops in songs.
- Ability to view generated lyrics illustrations (a telegram bot feature using the GPT).
- Ability to add own song chords or own versions of existing chords and make them public or private.
- Sharing private chords with certain people by one-time invitation only.
- Keywords are highlighted in search results. Local library search works instantly, no connection required. The search query is split into words as keywords, whole phrases or whole words are not needed.
- I couldn't choose a logo, so 27 logos switch automatically with the nice header animation.
- Flexible library filters and views: by user shortlists, tags, artist grouping, club rating, personal rating, with or without illustrations, with personal versions, etc.
- Permanent links for club songs without the webassembly, with a separate static website.

## Other features
- A Telegram Bot that:
  - Allows the club admin to maintain the library.
  - Maintains a telegram forum topic with the songs in sync with the library, handles shortlist buttons directly from telegram.
  - Allows to generate illustrations for the songs based on the lyrics. Multiple moods supported.
  - Users can chat with the bot in one of the telegram forum topics. The bot responds with text and images where appropriate (OpenAI GPT and image generation models).
  - OpenAI API usage is logged for analysis by admins.
  - Supports commands in PM (by admins, tenant admins, regular users).
  - Supports song inline search.
  - Gathers chord links from the telegram group and adds them to the library on behalf of the user who posted the link.

## Technical features
- The backend is hosted on Azure, using functions. Consumption plan. Currently it is fully within the lifetime free tier. The backend hosts the frontend and the telegram bots for the clubs.
- Functions are always kept hot.
- Full CI/CD with Azure pipelines.
- Data is stored in Cosmos DB, Azure SQL, Azure Storage. Queues are used for the telegram bot.
- Front end:
  - Indexed DB.
  - Notification of new version when it is ready. The service worker code is tweaked a bit to make the updates arrive faster.
  - Library updates only when changed (hash comparison saves traffic).
- Open AI:
  - Chat optimization to keep API usage costs low in an infinite dialog. GPT functions are used to detect when users change topics and the context can be cut.
- Chords and lyrics:
  - A unified model suitable for songs from different sources.
- All front ends are hosted statically, ASP.Net is not used.

## Projects and resources used
- The online tuner based on web audio api, https://github.com/qiuxiang/tuner
- Limitless - Responsive Web Application Kit, https://themeforest.net/item/limitless-responsive-web-application-kit/13080328
- Materials from https://muzland.info/, https://muzland.ru/

## Thanks for the inspiration
- Adam Neely, https://www.youtube.com/@AdamNeely
- Нескучный Саунд, https://www.youtube.com/@NeSkuSound
- Vitaly Pavlenko, https://github.com/vpavlenko
