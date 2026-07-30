package rabbitmq

import "sync/atomic"

type ConnectionStatus struct {
	connected atomic.Bool
}

func (s *ConnectionStatus) Set(connected bool) { s.connected.Store(connected) }
func (s *ConnectionStatus) IsConnected() bool   { return s.connected.Load() }
