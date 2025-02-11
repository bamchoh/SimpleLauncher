package main

import (
	"fmt"
	"log"

	"github.com/getlantern/systray"

	"golang.design/x/hotkey"
	"golang.design/x/hotkey/mainthread"

	_ "embed"
)

//go:embed favicon.ico
var favicon []byte

func onReady() {
	systray.SetIcon(favicon)

	systray.SetTitle("Simple Launcher")
	systray.SetTooltip("Simple Launcher")
	mQuit := systray.AddMenuItem("Quit", "Quit the whole app")
	go func() {
		<-mQuit.ClickedCh
		fmt.Println("Requesting quit")
		systray.Quit()
		fmt.Println("Finished quitting")
	}()
}

func onExit() {
}

func fn() {
	hk := hotkey.New([]hotkey.Modifier{hotkey.ModCtrl, hotkey.ModAlt}, hotkey.KeyO)

	for {
		err := hk.Register()
		if err != nil {
			log.Fatalf("hotkey: failed to register hotkey: %v", err)
			return
		}
		log.Printf("hotkey: %v is registered\n", hk)
		<-hk.Keydown()
		log.Printf("hotkey: %v is down\n", hk)
		<-hk.Keyup()
		log.Printf("hotkey: %v is up\n", hk)
		hk.Unregister()
		log.Printf("hotkey: %v is unregistered\n", hk)
	}
}

func main() {
	go func() {
		log.Println("Init")
		mainthread.Init(fn)
	}()

	log.Println("Run")
	systray.Run(onReady, onExit)
}
