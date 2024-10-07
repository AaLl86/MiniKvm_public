/*  AaLl86 MiniKvm
 *  
 *  Copyright© 2024 by Andrea Allievi 
 *  
 *  Module: RelaySensor_board.ino
 *  
 *  Description: 
 *     Implement the Power switch (for four target) using an Arduino Uno controller
 *     
 *  History:
 *     5/29/2024 - Initial implementation
 */              

const int Sensor_pin = 11;

const int RelayPins[] = {5, 2, 3, 4};
const int RelayOnState[] = {LOW, HIGH, LOW, HIGH};
const int SwitchCount = 4;

int Motion_state = LOW;
bool GlobalEcho = true;
bool GlobalDbg = false;

String Appliances[SwitchCount];
String CommandString;

#define ARRAYSIZE(x) sizeof(x) / sizeof(x[0])

void setup() {
  String Desc;

  Serial.begin(9600);             // Initialize the Serial port

  // Set the pins
  pinMode(Sensor_pin, INPUT);
  for (int Index = 0; Index < SwitchCount; Index++) {
    pinMode(RelayPins[Index], OUTPUT);
    digitalWrite(RelayPins[Index], LOW);
  }

  for (int Index = 0; Index < ARRAYSIZE(Appliances); Index++) {
    Appliances[Index] = "";
  }

  CommandString = "";

  if (Serial) {
     Serial.println("AaLl86 Relay Control Server v0.2\r\n");
  }

  // Triggers detectMotion function on rising mode to turn the relay on, if the condition is met
  // attachInterrupt(digitalPinToInterrupt(Sensor_pin), detectMotion, RISING);
}

//
// Sensor management routine
//
// Returns: 
//    TRUE if motion has been detected, FALSE otherwise.
//

bool ManageSensor() {
  int val = 0;
  val = digitalRead(Sensor_pin);

  if (val == HIGH) {
    if (Motion_state == LOW) {
      // Motion has been detected
      Serial.println("MOTION: Motion has been detected!");
      Motion_state = HIGH;
    }
  }
  else {
    if (Motion_state == HIGH) {
      // Motion has been stopped
      //Serial.println("Motion stopped.");
      Motion_state = LOW;
    }
  }
  return (Motion_state == HIGH);
}

void ShowCommandLineUsage() {
    Serial.println("Too lazy to write this routine.");
}

bool ProcessCommand(String Command) {
  String Cmd;
  String Out;
  String NewState;
  String Description;
  int SwitchNum = -1;
  int RelayPin = 0;
  enum {
    CMD_INVALID = 0,
    CMD_HELP,
    CMD_SWITCH,
    CMD_RESTART,
    CMD_DESC,
    CMD_STATE,
    CMD_ECHO,
    CMD_DEBUG,
    CMD_OTHER
  } CommandType;

  Cmd = Command;
  CommandType = CMD_INVALID;
  Description = ""; 
  NewState = "";
  Out = "";

  Cmd.toLowerCase();
  Cmd.trim();
  Command.trim();

  //
  // Parse commands here
  //

  if ((Cmd.indexOf("switch_") == 0) || (Cmd.indexOf("switch ") == 0)) {
    SwitchNum = Cmd.substring(7,8).toInt();
    NewState = Cmd.substring(9);
    CommandType = CMD_SWITCH;
  } 
  
  else if ((Cmd.indexOf("restart ") == 0) ||
           (Cmd.indexOf("reset ") == 0)) {
    SwitchNum = Cmd.substring(Cmd.indexOf(" ") + 1).toInt();
    NewState = "restart";
    CommandType = CMD_RESTART;
  }

  else if (Cmd.indexOf("desc ") == 0) {
    if (Cmd.substring(5, 11).equals("switch") == true) {
      RelayPin = 12;
    } else {
      RelayPin = 5;
    }

    if (Cmd.length() > RelayPin) {
       SwitchNum = Cmd[RelayPin] - 0x30;
       Description = Command.substring(RelayPin + 1);
    }

    if (Description != "") {
      Description.trim();
      if (Description[0] == '\"') {
        Description.remove(0, 1);
        Description.remove(Description.length() - 1, 1);
      }
    }
    CommandType = CMD_DESC;
  }

  else if (Cmd.indexOf("state") == 0) {
    if (Cmd.length() > 6) {
      SwitchNum = Cmd.substring(6,7).toInt();
      if (SwitchNum < 1 || SwitchNum > 4) { 
        SwitchNum = -1;
      }
    }

    CommandType = CMD_STATE;
  }

  else if (Cmd.indexOf("echo") == 0) {
    NewState = Cmd.substring(5);
    NewState.trim();
    CommandType = CMD_ECHO;
  }  

  else if (Cmd.indexOf("debug") == 0) {
    NewState = Cmd.substring(6);
    NewState.trim();
    CommandType = CMD_DEBUG;
  }

  else if ((Cmd.indexOf("?") == 0) ||
           (Cmd.indexOf("help") == 0)) {

    CommandType = CMD_HELP;
    ShowCommandLineUsage();
  }

  else if (Cmd.indexOf("ciao") == 0) {
    CommandType = CMD_OTHER;
    Serial.println("Buon giorno, buona sera e buona notte, animale!");
  }
  
  else if ((Cmd.indexOf("fuck you") == 0) ||
           (Cmd.indexOf("fuck off") == 0) ||
           (Cmd.indexOf("suca") == 0) ||
           (Cmd.indexOf("ciupa") == 0)) {
     
    Serial.println("Ma serio? Puzzi talmente tanto che il tuo fetore si sente fino a Seattle :-(");
    CommandType = CMD_OTHER;
  }

  else {
    // Invalid command received!
    Serial.println("Invalid command received!");
  }

  //
  // Check the parameters
  //

  if ((CommandType == CMD_SWITCH) ||
      (CommandType == CMD_RESTART) ||
      (CommandType == CMD_DESC)) {

      if (SwitchNum < 1 || SwitchNum > 4) { 
        Serial.println("Idiot, you have specified an invalid switch number.");
        CommandType = CMD_INVALID;
      }
  }

  //
  // Parse the actions here
  //

  if ((CommandType == CMD_SWITCH) || (CommandType == CMD_RESTART)) {
    int OnState = LOW;
    int OffState = HIGH;
    NewState.trim();
    
    // Calculate the correct Relay pin
    RelayPin = RelayPins[SwitchNum - 1];
    OnState = RelayOnState[SwitchNum - 1];
    if (OnState == HIGH) {OffState = LOW;}

    if (NewState.equals("0") || NewState.equals("off")) {
      // REMEMBER: The appliance is normally connected to the NO pin
      digitalWrite(RelayPin, OffState);
      Out = (String)"Switch " + SwitchNum + " set to OFF.";  
    } else if (NewState.equals("1") || NewState.equals("on")) {
      digitalWrite(RelayPin, OnState);
      Out = (String)"Switch " + SwitchNum + " set to ON.";  
    } else if (NewState.equals("restart")) {

      //
      // Restart means put in off state, wait a timeout of 0,8 or 5 seconds (depending on
      // the switch type), and put on again
      //

      Serial.print((String)"Restarting switch #" + SwitchNum + "...");

      if (OnState == HIGH) {
        digitalWrite(RelayPin, OnState);
        delay(800);
        digitalWrite(RelayPin, OffState);
      } else {
        digitalWrite(RelayPin, OffState);
        delay(5000);
        digitalWrite(RelayPin, OnState);
      }

      Out = "Done!";
    } else {
      Serial.println("Idiot, you have specified an invalid state (ON or OFF)!");
      CommandType == CMD_INVALID;
    }

    if (CommandType != CMD_INVALID) {
      Serial.println(Out);
    }
  }

  else if (CommandType == CMD_ECHO) {
    if (NewState.equals("0") || NewState.equals("off")) {
      GlobalEcho = false;
      Serial.println("Echo set to OFF.");
    } else if (NewState.equals("1") || NewState.equals("on")) {
      GlobalEcho = true;
      Serial.println("Echo set to ON.");
    } else {
      Serial.print("Echo is currently set to ");
      Serial.println(GlobalEcho);
    }
  }

  else if (CommandType == CMD_DEBUG) {
    if (NewState.equals("0") || NewState.equals("off")) {
      GlobalDbg = false;
      Serial.println("Debug set to OFF.");
    } else if (NewState.equals("1") || NewState.equals("on")) {
      GlobalDbg = true;
      Serial.println("Debug set to ON.");
    } else {
      Serial.print("Debug is currently set to ");
      Serial.println(GlobalDbg);
    }
  }

  else if (CommandType == CMD_DESC) {
    if (Description != "") {
      Appliances[SwitchNum - 1] = Description;
    }

    NewState = "Switch ";
    NewState += SwitchNum;
    if (RelayOnState == HIGH) {
      NewState += " (low power) ";
    }
    NewState += " description set to \"";
    NewState += Appliances[SwitchNum - 1];
    NewState += "\".";
    Serial.println(NewState);
  }

  else if (CommandType == CMD_STATE) {
    int PinState = 0;
    int NumberOfReads = 0;
    bool DisplayString = false;

    if (SwitchNum > 0) {
      NumberOfReads = 1;
    } else {
      SwitchNum = 1;
      NumberOfReads = SwitchCount;
      DisplayString = true;
    }

    while (NumberOfReads > 0) {
      // Calculate the correct Relay pin
      RelayPin = RelayPins[SwitchNum - 1];

      PinState = digitalRead(RelayPin);
      NewState = "";

      if (DisplayString != false) {
        NewState = "Switch " + String(SwitchNum);
        if (Appliances[SwitchNum - 1] != "") {
          NewState += " (";
          NewState += Appliances[SwitchNum - 1];
          NewState += ")";
        } else {
          if (RelayOnState[SwitchNum - 1] == HIGH) {
            NewState += " (low power)";
          }
        }
        NewState += " ";
      }

      if (PinState == RelayOnState[SwitchNum - 1]) {
        NewState += "ON";
      } else {
        NewState += "OFF";
      }
      Serial.println(NewState);

      NumberOfReads -= 1;
      SwitchNum += 1;
    }
  }

  //
  // Write debug information if needed.
  //

  if (GlobalDbg != false) {
    Serial.println("");
    if (SwitchNum != -1) {
      Serial.println("DBG: Switch number " + String((int)SwitchNum));
    }
    if (NewState != "") {
      Serial.println("DBG: NewState: " + NewState);
    }
    if (Description != "") {
      Serial.println("DBG: Description: " + Description);
    }
    
    Serial.print("DBG: Command type: " + String((int)CommandType));
    Serial.println(" - Full command: \"" + Cmd + "\".\n");
  }

  return (CommandType > CMD_INVALID);
}


bool ReadAndProcessData() {
  char ReadChar;
  int StrSize;
  int timeout = 0;
  bool ValidCommand = false;

  if (Serial.available() == false) { 
    return false; 
  }

  while (true) {
    if (Serial.available() == false) {
       if (timeout >= 1000) {
          // Timeout happened, bail out
          return false;
       } 
       timeout += 100;
       delay(100);
       continue;
    }

    ReadChar = Serial.read();

    /* Uncomment the following block to discover what character is read in HEX 
    //itoa((int)ReadChar, IntChar, 10);
    String IntStr = String((int)ReadChar, HEX);
    Serial.print(" " + IntStr + " "); */
    
    if ((int)ReadChar == 0x7F) {
      StrSize = CommandString.length();
    
      // Backspace has been read, clean up the last character from the string if it exists
      if (StrSize > 0) {
        CommandString.remove(StrSize - 1);
      }
   
    } else if ((ReadChar == '\r') || (ReadChar == '\n')) {

      // Reached a newline character, parse the complete command line here
      ValidCommand = ProcessCommand(CommandString);

      CommandString.remove(0);

    } else {
      CommandString += ReadChar;
    }

    if (GlobalEcho == true) Serial.write(ReadChar);
  }    

  return ValidCommand;
}

void loop() {
  ReadAndProcessData();
  //ManageSensor();
}
