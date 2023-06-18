/*
 * UART.c
 *
 * Created: 21/03/2023 9:28:54 PM
 *  Author: Kirollos
 */ 
#include <avr/io.h>
#include "UART.h"
#include <avr/interrupt.h>
static volatile uint8_t *tx_buffer;
static volatile uint16_t tx_len;
static volatile uint16_t tx_cnt;
static volatile uint8_t *rx_buffer;
static volatile uint16_t rx_len;
static volatile uint16_t rx_cnt;
ISR(USART_RX_vect) {
	uint8_t rx_data;
	cli();
	/* Read rx_data. */
	rx_data = UDR0;
	/* Ignore spaces */
	if((rx_cnt < rx_len) && (rx_data != ' ')) {
		rx_buffer[rx_cnt] = rx_data;
		rx_cnt++;
	}
	sei();
}

ISR(USART_TX_vect) {
	cli();
	tx_cnt++;
	if(tx_cnt < tx_len) {
		/* Send next byte. */
		UDR0 = tx_buffer[tx_cnt];
	}
	sei();
}
void UART_Init(UART_cfg *my_cfg) {
	/* Set baud rate */
	UBRR0H = my_cfg->UBRRH_cfg;
	UBRR0L = my_cfg->UBRRL_cfg;
	UCSR0A = my_cfg->UCSRA_cfg;
	UCSR0B = my_cfg->UCSRB_cfg;
	UCSR0C = my_cfg->UCSRC_cfg;
}
void UART_SendPayload(uint8_t *tx_data, uint16_t len) {
	tx_buffer = tx_data;
	tx_len = len;
	tx_cnt = 0;
	/* Wait for UDR is empty. */
	while(0 == (UCSR0A & (1 << UDRE0)));
	/* Send the first byte to trigger the TxC interrupt. */
	UDR0 = tx_buffer[0];
}
void UART_ReceivePayload(uint8_t *rx_data, uint16_t len) {
	rx_buffer = rx_data;
	rx_len = len;
	rx_cnt = 0;
}
uint8_t UART_IsTxComplete(void) {
	return ( (tx_cnt >= tx_len) ? 1 : 0 );
}
uint8_t UART_IsRxComplete(void) {
	return ( (rx_cnt >= rx_len) ? 1 : 0 );
}