I created this because i have an artnet controller and wireless dmx lights i wanted to use in my home, you can create presets for fixtures and groups of fixtures, with live feedback when setting up, that then can be saved and recalled either by a set time or by astronomical time (sunrise/sunset by zip code or exact location).

this also has a fully functional api if you want to integrate artnet control into your application in a lightweight manner, in my case im using a tcp socket from a crestron system, but this should be easy from homeassistant or nearly anything else that can send a webrequest, just make a preset, and then on the live control page theres a copy curl command button that will give you exactly what you need

docker compose will look as such

services:
  artnet:
    image: ghcr.io/jp-tx/artnetdmxlights:latest
    restart: always
    ports:
      - (your port, http NOT https, if you want that use a reverse proxy):8080
networks: {}

