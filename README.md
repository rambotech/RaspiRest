# RaspiRest
An aspnetcore demo for flashing an LED via GPIO on a Raspberry PI. The focus of this project is using an IHostedService to handle GPIO
and notification activity, which occur on background timer threads and are independent of controller actions.

This is an ideal architecture for hosting a Restful API to receive (and act upon) webhooks sent from IFTTT.com (If This, Then That). The
example here runs on a Raspberry Pi Model 3+ in my house, with a port open in the router for access.  More about that shortly.

## Background
This project uses an always on green LED (wired between +5V and GND), and a variant yellow LED controlled by a GPIO pin.  This is a typical electronics project for a breadboard.
The Green LED is optional, but useful to see that you have the LED properly wired from +5V to GND.

The project contains pre-defined endpoints for the following from an IFTTT applet triggered by a Google Assistant (or Amazon Alexa) voice command:
- "execute order ..." .. captures the text after executive order, which the endpoint can process and act on. This example only logs the text received.
- "test motion detect" .. triggers a webhook sent to IFTTT, which triggers a gmail message with canned text (or whatever THAT action the applet defines).
- "set light on"
- "set "light off"
- "set light toggle" .. to set a static state of the yellow light.
- "set light flash"
- "set light flash to slow"  (or fast, help, mayday, beer) .. to set a specific flashing pattern.

The "set light" actions control the GPIO interface, which triggers the LED action.  Flash actions use a timer with varying delays to determine the pattern.

## Building the circuit on the bread board
You will need the following parts:
- 40-pin T-connector (optional, shown in diagram), or three F-to-M connector wires to connect the ping from the 40-pin interface to the breadboard.
- Green LED
- Yellow LED
- (2) 220 Ohm 1/4 watt resistors.
- a few M-to-M breadboard wires to connect the circuit

The wiring on the breadboard is done like this:

![alt text](https://github.com/rambotech/RaspiRest/blob/master/assets/Fritzing_Breadboard_Diagram.png)

NOTE: Pin 12 is GPIO #18, which controls the Yellow LED through an on (5 VDC) and off state of that pin.
Sharp eyes will notice that the Flash.cs file in Entity defaults this to Pin 18.  The setting is overriden
in the contructor, using a IConfiguration


# Preapring the Pi

You will need to have .NET Core 2.1 installed on the Raspberry Pi 3+.  You can following the instructions here: 

![alt text](https://github.com/rambotech/RaspiRest/blob/master/assets/IFTTT_Applets.png)



![alt text](https://github.com/rambotech/RaspiRest/blob/master/assets/IFTTT_Command.png)



![alt text](https://github.com/rambotech/RaspiRest/blob/master/assets/IFTTT_Flash.png)


